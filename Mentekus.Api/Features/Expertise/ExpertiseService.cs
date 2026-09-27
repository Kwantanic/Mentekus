using System.Data;
using System.Text.Json;
using Dapper;
using Mentekus.Api.Features.Expertise.Entities;
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

        // Optional generate + graph upsert; non-fatal per spec (pure vector path continues to work).
        // Narrow LLM try per review (Issue 2): only extract/generate is "LLM"; graph upserts use separate/general non-fatal handling.
        string[]? topics = null;
        try
        {
            topics = await ExtractTopicsInternalAsync(text, sourceType, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM topic extraction failed for user {UserId} source {SourceType}; vector update succeeded.", userId, sourceType);
            // Do not throw: error handling for partial LLM failure (vector path unaffected)
        }

        if (topics != null && topics.Length > 0)
        {
            var weight = GetWeightForSource(sourceType);
            foreach (var raw in topics.Take(5))
            {
                var topicName = raw?.Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(topicName))
                {
                    try
                    {
                        await UpsertTopicAndStrengthAsync(userId, topicName, weight, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // Graph maintenance (DB upsert) failure is also best-effort/non-fatal; use general message (not "LLM")
                        logger.LogWarning(ex, "Topic graph maintenance failed (non-fatal) after vector update for user {UserId} source {SourceType} topic {Topic}.", userId, sourceType, topicName);
                    }
                }
            }
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

    public async Task<string> IngestDocumentAsync(string text, Guid userId, CancellationToken cancellationToken = default)
    {
        var embedding = await ollamaAdapter.EmbedAsync(text, cancellationToken);
        if (embedding == null || embedding.Length == 0)
            throw new EmbeddingFailedException("Failed to generate embedding for the document.");

        await UpdateFromContributionAsync(userId, embedding, text, ExpertiseSql.DocumentSourceType, cancellationToken: cancellationToken);

        return "Document ingested. Expertise updated.";
    }

    public async Task<List<ExpertiseRouteMatch>> RouteExpertsAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit <= 0 ? 10 : limit, 1, 50);

        var embedding = await ollamaAdapter.EmbedAsync(query, cancellationToken);
        if (embedding == null || embedding.Length == 0)
            return [];

        var vector = new Pgvector.Vector(embedding);

        var candidates = (await connection.QueryAsync<UserRoutingRow>(
            ExpertiseSql.FindRoutableUsersWithEmbedding,
            new { Vector = vector, Limit = Math.Min(limit * 3, 50) })).ToList();

        if (candidates.Count == 0)
            return [];

        // Extract topics for query to compute MatchedTopics (uses generate, may be mocked)
        string[] queryTopics = [];
        try
        {
            queryTopics = await ExtractTopicsInternalAsync(query, ExpertiseSql.QuestionSourceType, cancellationToken);
        }
        catch
        {
            queryTopics = [];
        }

        var matches = new List<ExpertiseRouteMatch>();
        foreach (var cand in candidates)
        {
            var vecSim = ComputeCosineSimilarity(embedding, cand.ExpertiseEmbedding);
            var userTopics = (await connection.QueryAsync<TopicStrengthRow>(
                ExpertiseSql.GetUserTopicsForMatch,
                new { UserId = cand.Id, Limit = 20 })).Select(t => t.Name).ToArray();

            var matched = queryTopics.Intersect(userTopics, StringComparer.OrdinalIgnoreCase).ToArray();

            // Hybrid score: vec sim primary + bonus for topic overlap (enables promotion of lower-vec but higher-overlap candidates thanks to overfetch)
            var topicBonus = matched.Length > 0 ? Math.Min(matched.Length * 0.05, 0.2) : 0.0;
            var score = Math.Clamp(vecSim + topicBonus, 0.0, 1.0);
            var confidence = Math.Clamp(vecSim, 0.0, 1.0);

            matches.Add(new ExpertiseRouteMatch(
                cand.Id,
                cand.Name,
                cand.Email,
                score,
                vecSim,
                matched,
                confidence));
        }

        // Re-rank by hybrid score desc, then take the requested limit (overfetch + re-rank allows topic bonus to promote candidates outside pure-vec top-N)
        matches = matches.OrderByDescending(m => m.Score).ThenByDescending(m => m.VecSim).Take(limit).ToList();
        return matches;
    }

    private static double ComputeCosineSimilarity(float[] queryEmb, Pgvector.Vector? userVec)
    {
        if (userVec == null) return 0.0;
        var u = userVec.ToArray();
        if (u.Length != queryEmb.Length) return 0.0;
        // Compute cosine similarity via dot product / norms (embeddings are positive ~unit; result in [0,1]).
        // Matches the semantic intent of pgvector <=> ordering used for candidates (higher sim = better).
        double dot = 0, qn = 0, un = 0;
        for (int i = 0; i < queryEmb.Length; i++)
        {
            dot += queryEmb[i] * u[i];
            qn += queryEmb[i] * queryEmb[i];
            un += u[i] * u[i];
        }
        var qNorm = Math.Sqrt(qn);
        var uNorm = Math.Sqrt(un);
        if (qNorm == 0 || uNorm == 0) return 0;
        return dot / (qNorm * uNorm);
    }

    private async Task UpdateVectorInternalAsync(Guid userId, float[] embedding, string sourceType, float? alphaOverride, CancellationToken cancellationToken)
    {
        int dim = ExpertiseSql.EmbeddingDimension;
        if (embedding == null || embedding.Length == 0)
            return;
        if (embedding.Length != dim)
            throw new ArgumentException($"Embedding must be {dim}-dimensional (got {embedding.Length}).", nameof(embedding));

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
            {
                // Rare (visibility + concurrent delete race); log for observability before skipping weight (Issue 7)
                logger.LogDebug("Topic re-query after upsert returned no ID for name {TopicName} (skipping strength update for user {UserId}).", topicName, userId);
                return;
            }
            topicId = re.Value;
        }

        await connection.ExecuteAsync(ExpertiseSql.UpdateUserTopicStrength,
            new { UserId = userId, TopicId = topicId, Strength = weight });
    }

    private static float[] BlendExpertiseVector(float[]? current, float[] contribution, float alpha, DateTime? lastUpdatedUtc = null)
    {
        int dim = ExpertiseSql.EmbeddingDimension;
        alpha = Math.Clamp(alpha, 0f, 1f);

        // Guard contribution dim (bug fix Issue 1): prevent IndexOutOfRange on malformed/wrong-dim embed before any indexing.
        // (Caller also guards null/empty, but Blend is the math core and may be called directly in future/tests.)
        if (contribution == null || contribution.Length != dim)
        {
            throw new ArgumentException($"Embedding contribution must be {dim}-dimensional (got {contribution?.Length ?? 0}).", nameof(contribution));
        }

        // No Clone(): ToArray() from caller already provides a fresh array; decay mutation is safe and local (Issue 3).
        var curr = (current != null && current.Length == dim) ? current : new float[dim];

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
}