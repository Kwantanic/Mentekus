using System.Data;
using System.Text.Json;
using Dapper;
using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Mentekus.Api.Shared.Adapters;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace Mentekus.Api.Features.Expertise;

[RegisterScoped(ServiceType = typeof(IExpertiseService))]
public class ExpertiseService(
    IOllamaAdapter ollamaAdapter,
    IDbConnection connection,
    ILogger<ExpertiseService> logger) : IExpertiseService
{
    public async Task UpdateVectorOnlyFromContributionAsync(Guid userId, float[] embedding, string sourceType, float? alphaOverride = null, CancellationToken cancellationToken = default)
    {
        await UpdateVectorInternalAsync(userId, embedding, sourceType, alphaOverride, cancellationToken);
    }

    public async Task UpdateFromContributionAsync(Guid userId, float[] embedding, string text, string sourceType, float? alphaOverride = null, CancellationToken cancellationToken = default)
    {
        await UpdateVectorInternalAsync(userId, embedding, sourceType, alphaOverride, cancellationToken);

        // Optional generate + graph upsert; non-fatal per spec (pure vector path continues to work)
        try
        {
            var topics = await ExtractTopicsInternalAsync(text, sourceType, cancellationToken);
            if (topics.Length > 0)
            {
                var weight = GetWeightForSource(sourceType);
                foreach (var raw in topics.Take(5))
                {
                    var topicName = raw?.Trim().ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(topicName))
                    {
                        await UpsertTopicAndStrengthAsync(userId, topicName, weight, cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM topic extraction failed for user {UserId} source {SourceType}; vector update succeeded.", userId, sourceType);
            // Do not throw: error handling for partial LLM failure
        }
    }

    public async Task<string[]> ExtractTopicsAsync(string text, string? sourceType = null, CancellationToken cancellationToken = default)
    {
        return await ExtractTopicsInternalAsync(text, sourceType ?? ExpertiseSql.QuestionSourceType, cancellationToken);
    }

    public async Task<UserExpertiseProfile?> GetUserExpertiseAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var row = await connection.QuerySingleOrDefaultAsync<UserExpertiseRow>(
            ExpertiseSql.FindUserExpertise, new { UserId = userId });

        if (row == null)
            return null;

        var topicRows = await connection.QueryAsync<TopicStrengthRow>(
            ExpertiseSql.GetUserTopics, new { UserId = userId, Limit = 10 });

        var topTopics = topicRows.Select(t => t.Name).ToArray();

        return new UserExpertiseProfile(
            row.UserId,
            row.Name,
            row.Email,
            row.ExpertiseSummary,
            topTopics,
            row.LastExpertiseUpdate);
    }

    private async Task UpdateVectorInternalAsync(Guid userId, float[] embedding, string sourceType, float? alphaOverride, CancellationToken cancellationToken)
    {
        if (embedding == null || embedding.Length == 0)
            return;

        var row = await connection.QuerySingleOrDefaultAsync<UserEmbeddingRow>(
            ExpertiseSql.GetUserExpertiseEmbedding, new { UserId = userId });

        var current = row?.ExpertiseEmbedding?.ToArray();
        var lastUpdated = row?.LastExpertiseUpdate;

        var alpha = alphaOverride ?? GetAlphaForSource(sourceType);
        var blended = BlendExpertiseVector(current, embedding, alpha, lastUpdated);

        var vector = new Vector(blended);
        await connection.ExecuteAsync(ExpertiseSql.UpdateUserExpertiseEmbedding,
            new { UserId = userId, Embedding = vector });
    }

    private async Task<string[]> ExtractTopicsInternalAsync(string text, string sourceType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var template = sourceType == ExpertiseSql.DocumentSourceType
            ? ExpertiseSql.DocumentTopicExtractionPromptTemplate
            : ExpertiseSql.GeneralTopicExtractionPromptTemplate;

        var truncated = TruncateForPrompt(text);
        var prompt = string.Format(template, truncated);

        var result = await ollamaAdapter.GenerateAsync(prompt, cancellationToken);
        return ParseTopicsDefensively(result);
    }

    private async Task UpsertTopicAndStrengthAsync(Guid userId, string topicName, float weight, CancellationToken cancellationToken)
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

            // Re-query in case of concurrent insert race on unique name
            var re = await connection.ExecuteScalarAsync<Guid?>(
                ExpertiseSql.GetTopicIdByName, new { Name = topicName });
            if (!re.HasValue)
                return;
            topicId = re.Value;
        }

        await connection.ExecuteAsync(ExpertiseSql.UpdateUserTopicStrength,
            new { UserId = userId, TopicId = topicId, Strength = weight });
    }

    private static float[] BlendExpertiseVector(float[]? current, float[] contribution, float alpha, DateTime? lastUpdatedUtc = null)
    {
        const int dim = 1024;
        alpha = Math.Clamp(alpha, 0f, 1f);

        var curr = (current != null && current.Length == dim) ? (float[])current.Clone() : new float[dim];

        if (lastUpdatedUtc.HasValue)
        {
            var days = (DateTime.UtcNow - lastUpdatedUtc.Value).TotalDays;
            if (days > 0)
            {
                // Exponential decay, 90-day half-life
                var decay = (float)Math.Pow(0.5, days / 90.0);
                for (int i = 0; i < dim; i++)
                    curr[i] *= decay;
            }
        }

        var blended = new float[dim];
        for (int i = 0; i < dim; i++)
        {
            blended[i] = alpha * contribution[i] + (1f - alpha) * curr[i];
        }

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
                // Attempt to extract JSON array substring defensively
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
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        list.Add(s.Trim());
                }
            }
            return list.ToArray();
        }
        catch
        {
            return [];
        }
    }

    // Internal row types for Dapper projections (no AOT serializer needed)
    private sealed record UserEmbeddingRow(Vector? ExpertiseEmbedding, DateTime? LastExpertiseUpdate);

    private sealed record UserExpertiseRow(Guid UserId, string Name, string Email, Vector? ExpertiseEmbedding, string? ExpertiseSummary, DateTime? LastExpertiseUpdate);

    private sealed record TopicStrengthRow(string Name, float Strength, DateTime LastUpdated);
}