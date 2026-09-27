using System.Data;
using System.Text.Json;
using Dapper;
using Mentekus.Api.Features.Expertise.Entities;
using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Mentekus.Api.Shared.Adapters;
using Npgsql;
using Pgvector;

namespace Mentekus.Api.Features.Expertise;

[RegisterScoped(ServiceType = typeof(IExpertiseService))]
public class ExpertiseService(
    IOllamaAdapter ollamaAdapter,
    NpgsqlDataSource dataSource,
    ITopicExtractionQueue topicQueue,
    ILogger<ExpertiseService> logger) : IExpertiseService
{
    public async Task UpdateVectorOnlyFromContributionAsync(Guid userId, float[] embedding, string sourceType, float? alphaOverride = null, CancellationToken cancellationToken = default)
    {
        if (embedding == null || embedding.Length == 0)
            return;

        EmbeddingSize.Require(embedding);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await BlendContributionAsync(connection, transaction, userId, embedding, sourceType, alphaOverride, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateFromContributionAsync(Guid userId, float[] embedding, string text, string sourceType, float? alphaOverride = null, CancellationToken cancellationToken = default)
    {
        await UpdateVectorOnlyFromContributionAsync(userId, embedding, sourceType, alphaOverride, cancellationToken);
        await EnqueueTopicsAsync(userId, text, sourceType, cancellationToken);
    }

    public async Task BlendContributionAsync(IDbConnection connection, IDbTransaction transaction, Guid userId, float[] embedding, string sourceType, float? alphaOverride = null, CancellationToken cancellationToken = default)
    {
        if (embedding == null || embedding.Length == 0)
            return;

        EmbeddingSize.Require(embedding);
        var row = await connection.QuerySingleOrDefaultAsync<UserEmbeddingRow>(
            ExpertiseSql.GetUserExpertiseEmbeddingForUpdate,
            new { UserId = userId },
            transaction);

        var current = row?.ExpertiseEmbedding?.ToArray();
        var alpha = alphaOverride ?? GetAlphaForSource(sourceType);
        var blended = BlendExpertiseVector(current, embedding, alpha, row?.LastExpertiseUpdate);
        await connection.ExecuteAsync(
            ExpertiseSql.UpdateUserExpertiseEmbedding,
            new { UserId = userId, Embedding = new Vector(blended) },
            transaction);
    }

    public async Task EnqueueTopicsAsync(Guid userId, string text, string sourceType, CancellationToken cancellationToken = default)
    {
        await topicQueue.EnqueueAsync(userId, TruncateForPrompt(text), sourceType, cancellationToken);
    }

    public async Task ApplyTopicsAsync(Guid userId, string text, string sourceType, CancellationToken cancellationToken = default)
    {
        string[] topics;
        try
        {
            topics = await ExtractTopicsInternalAsync(text, sourceType, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Topic extraction failed for user {UserId} source {SourceType}.", userId, sourceType);
            return;
        }

        if (topics.Length == 0)
            return;

        var weight = GetWeightForSource(sourceType);
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            foreach (var raw in topics.Take(5))
            {
                var topicName = raw.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(topicName))
                    continue;

                try
                {
                    await UpsertTopicAndStrengthAsync(connection, userId, topicName, weight);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception,
                        "Topic graph maintenance failed for user {UserId} source {SourceType} topic {Topic}.",
                        userId, sourceType, topicName);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Topic graph maintenance failed for user {UserId} source {SourceType}.", userId, sourceType);
        }
    }

    public async Task<string[]> ExtractTopicsAsync(string text, string? sourceType = null, CancellationToken cancellationToken = default)
    {
        return await ExtractTopicsInternalAsync(text, sourceType ?? ExpertiseSql.QuestionSourceType, cancellationToken);
    }

    public async Task<UserExpertiseProfile?> GetUserExpertiseAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserExpertiseRow>(
            ExpertiseSql.FindUserExpertise, new { UserId = userId });

        if (row == null)
            return null;

        var topicRows = await connection.QueryAsync<TopicStrengthRow>(
            ExpertiseSql.GetUserTopics, new { UserId = userId, Limit = 10 });

        return new UserExpertiseProfile(
            row.UserId,
            row.Name,
            row.Email,
            row.ExpertiseSummary,
            topicRows.Select(topic => topic.Name).ToArray(),
            row.LastExpertiseUpdate);
    }

    public async Task<string> IngestDocumentAsync(string text, Guid userId, CancellationToken cancellationToken = default)
    {
        var embedding = await ollamaAdapter.EmbedAsync(text, cancellationToken);
        if (embedding == null || embedding.Length == 0)
            throw new EmbeddingFailedException("Failed to generate embedding for the document.");

        EmbeddingSize.Require(embedding);
        await UpdateFromContributionAsync(userId, embedding, text, ExpertiseSql.DocumentSourceType, cancellationToken: cancellationToken);
        return "Document ingested. Expertise updated.";
    }

    public async Task<List<ExpertiseRouteMatch>> RouteExpertsAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit <= 0 ? 10 : limit, 1, 50);

        var embeddingTask = ollamaAdapter.EmbedAsync(query, cancellationToken);
        var topicsTask = ExtractTopicsForRoutingAsync(query, cancellationToken);
        await Task.WhenAll(embeddingTask, topicsTask);

        var embedding = await embeddingTask;
        if (embedding == null || embedding.Length == 0)
            return [];

        var queryTopics = await topicsTask;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var candidates = (await connection.QueryAsync<UserRoutingRow>(
            ExpertiseSql.FindRoutableUsersWithEmbedding,
            new { Vector = new Vector(embedding), Limit = Math.Min(limit * 3, 50) })).ToList();

        var matches = new List<ExpertiseRouteMatch>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var userTopics = ParseTopicsDefensively(candidate.TopicsJson);
            var matched = queryTopics.Intersect(userTopics, StringComparer.OrdinalIgnoreCase).ToArray();
            var topicBonus = matched.Length > 0 ? Math.Min(matched.Length * 0.05, 0.2) : 0.0;
            var score = Math.Clamp(candidate.VecSim + topicBonus, 0.0, 1.0);
            var confidence = Math.Clamp(candidate.VecSim, 0.0, 1.0);
            matches.Add(new ExpertiseRouteMatch(
                candidate.Id,
                candidate.Name,
                candidate.Email,
                score,
                candidate.VecSim,
                matched,
                confidence));
        }

        return matches.OrderByDescending(match => match.Score).ThenByDescending(match => match.VecSim).Take(limit).ToList();
    }

    private async Task<string[]> ExtractTopicsForRoutingAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            return await ExtractTopicsInternalAsync(query, ExpertiseSql.QuestionSourceType, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Topic extraction failed during expert routing.");
            return [];
        }
    }

    private async Task UpsertTopicAndStrengthAsync(NpgsqlConnection connection, Guid userId, string topicName, float weight)
    {
        var existingId = await connection.ExecuteScalarAsync<Guid?>(
            ExpertiseSql.GetTopicIdByName, new { Name = topicName });

        Guid topicId;
        if (existingId.HasValue)
        {
            topicId = existingId.Value;
        }
        else
        {
            topicId = Guid.NewGuid();
            await connection.ExecuteAsync(ExpertiseSql.UpsertTopic,
                new { Id = topicId, Name = topicName, CreatedAt = DateTime.UtcNow });

            var reloaded = await connection.ExecuteScalarAsync<Guid?>(
                ExpertiseSql.GetTopicIdByName, new { Name = topicName });
            if (!reloaded.HasValue)
            {
                logger.LogDebug("Topic re-query after upsert returned no ID for name {TopicName} (skipping strength update for user {UserId}).", topicName, userId);
                return;
            }

            topicId = reloaded.Value;
        }

        await connection.ExecuteAsync(ExpertiseSql.UpdateUserTopicStrength,
            new { UserId = userId, TopicId = topicId, Strength = weight });
    }

    private async Task<string[]> ExtractTopicsInternalAsync(string text, string sourceType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var template = sourceType == ExpertiseSql.DocumentSourceType
            ? ExpertiseSql.DocumentTopicExtractionPromptTemplate
            : ExpertiseSql.GeneralTopicExtractionPromptTemplate;

        // Replace, not string.Format: contributor text may contain braces.
        var prompt = template.Replace("{0}", TruncateForPrompt(text), StringComparison.Ordinal);
        var result = await ollamaAdapter.GenerateAsync(prompt, cancellationToken);
        return ParseTopicsDefensively(result);
    }

    private static float[] BlendExpertiseVector(float[]? current, float[] contribution, float alpha, DateTime? lastUpdatedUtc = null)
    {
        int dim = ExpertiseSql.EmbeddingDimension;
        alpha = Math.Clamp(alpha, 0f, 1f);

        if (contribution == null || contribution.Length != dim)
            throw new ArgumentException($"Embedding contribution must be {dim}-dimensional (got {contribution?.Length ?? 0}).", nameof(contribution));

        var curr = new float[dim];
        if (current != null && current.Length == dim)
            current.AsSpan().CopyTo(curr);

        if (lastUpdatedUtc.HasValue)
        {
            var days = (DateTime.UtcNow - lastUpdatedUtc.Value).TotalDays;
            if (days > 0)
            {
                // Exponential decay, 90-day half-life. Magnitude is what fades, so the next blend weighs the new contribution more.
                var decay = (float)Math.Pow(0.5, days / 90.0);
                for (int i = 0; i < dim; i++)
                    curr[i] *= decay;
            }
        }

        var blended = new float[dim];
        for (int i = 0; i < dim; i++)
            blended[i] = alpha * contribution[i] + (1f - alpha) * curr[i];

        return blended;
    }

    private static float GetAlphaForSource(string sourceType) => sourceType switch
    {
        ExpertiseSql.DocumentSourceType => 0.25f,
        ExpertiseSql.AnswerSourceType => 0.15f,
        ExpertiseSql.QuestionSourceType => 0.05f,
        _ => 0.10f
    };

    private static float GetWeightForSource(string sourceType) => sourceType switch
    {
        ExpertiseSql.DocumentSourceType or ExpertiseSql.AnswerSourceType => 1.0f,
        _ => 0.3f
    };

    private static string TruncateForPrompt(string text, int maxChars = 4000)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxChars)
            return text;
        return text[..maxChars] + "...";
    }

    private static string[] ParseTopicsDefensively(string? result)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(result))
                return [];

            var trimmed = result.Trim();
            if (!trimmed.StartsWith('['))
            {
                var start = trimmed.IndexOf('[');
                var end = trimmed.LastIndexOf(']');
                if (start >= 0 && end > start)
                    trimmed = trimmed.Substring(start, end - start + 1);
                else
                    return [];
            }

            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var list = new List<string>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String)
                    continue;

                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    list.Add(value.Trim());
            }

            return list.ToArray();
        }
        catch
        {
            return [];
        }
    }
}
