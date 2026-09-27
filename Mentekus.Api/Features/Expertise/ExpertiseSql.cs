namespace Mentekus.Api.Features.Expertise;

public static class ExpertiseSql
{
    public const string DocumentSourceType = "Document";
    public const string AnswerSourceType = "Answer";
    public const string QuestionSourceType = "Question";

    /// <summary>Dimension of expertise embeddings (matches qwen3-embedding:0.6b output and VECTOR(1024) in schema).</summary>
    public const int EmbeddingDimension = 1024;

    /// <summary>Shared by the expertise vector and topic strength.</summary>
    public const int HalfLifeDays = 90;

    public const double HalfLifeSeconds = HalfLifeDays * 86400d;

    /// <summary>Decayed strength below this is omitted and deleted on the next contribution.</summary>
    public const double MinimumTopicStrength = 0.05;

    public const string GeneralTopicExtractionPromptTemplate = """
        From the following text (a question or answer or professional document/CV), extract 3-5 short, canonical expertise topics or skills as a strict JSON array of strings only. Use lowercase, concise phrases like "native aot" or "pgvector similarity". No other text or explanation. Text: {0}
        """;

    public const string DocumentTopicExtractionPromptTemplate = """
        From this CV or position description at an organisation, extract 4-8 concise expertise topics/skills (professional domains, technologies, roles, org context) as a strict JSON array of strings only. Example: ["senior dotnet engineer", "pgvector optimization", "acme corp backend"]. No other text.
        Text: {0}
        """;

    public const string GetUserExpertiseEmbeddingForUpdate = """
        SELECT ExpertiseEmbedding, LastExpertiseUpdate
        FROM Users
        WHERE Id = @UserId
        FOR UPDATE
        """;

    public const string UpdateUserExpertiseEmbedding = """
        UPDATE Users
        SET ExpertiseEmbedding = @Embedding, LastExpertiseUpdate = now()
        WHERE Id = @UserId
        """;

    public const string FindUserExpertise = """
        SELECT u.Id AS UserId, u.Name, u.Email, u.LastExpertiseUpdate
        FROM Users u
        WHERE u.Id = @UserId
        """;

    // Stored strength is the value at LastUpdated. Reads apply the same 90-day half-life as the vector.
    public const string GetUserTopics = """
        SELECT t.Name
        FROM UserTopicExpertise ute
        JOIN Topics t ON t.Id = ute.TopicId
        WHERE ute.UserId = @UserId
          AND ute.Strength * power(0.5::double precision, GREATEST(EXTRACT(EPOCH FROM (now() - COALESCE(ute.LastUpdated, now()))), 0) / @HalfLifeSeconds) >= @MinimumStrength
        ORDER BY ute.Strength * power(0.5::double precision, GREATEST(EXTRACT(EPOCH FROM (now() - COALESCE(ute.LastUpdated, now()))), 0) / @HalfLifeSeconds) DESC, t.Name
        LIMIT @Limit
        """;

    public const string UpsertTopic = """
        INSERT INTO Topics (Id, Name, CreatedAt)
        VALUES (@Id, @Name, @CreatedAt)
        ON CONFLICT (Name) DO NOTHING
        """;

    public const string GetTopicIdByName = """
        SELECT Id FROM Topics WHERE Name = @Name
        """;

    public const string UpdateUserTopicStrength = """
        INSERT INTO UserTopicExpertise (UserId, TopicId, Strength, LastUpdated)
        VALUES (@UserId, @TopicId, @Strength, now())
        ON CONFLICT (UserId, TopicId)
        DO UPDATE SET
            Strength = UserTopicExpertise.Strength
                * power(0.5::double precision, GREATEST(EXTRACT(EPOCH FROM (now() - COALESCE(UserTopicExpertise.LastUpdated, now()))), 0) / @HalfLifeSeconds)
                + EXCLUDED.Strength,
            LastUpdated = now()
        """;

    public const string PruneFadedTopics = """
        DELETE FROM UserTopicExpertise ute
        WHERE ute.UserId = @UserId
          AND ute.Strength * power(0.5::double precision, GREATEST(EXTRACT(EPOCH FROM (now() - COALESCE(ute.LastUpdated, now()))), 0) / @HalfLifeSeconds) < @MinimumStrength
        """;

    public const string UpdateExpertiseSummary = """
        UPDATE Users
        SET ExpertiseSummary = @Summary
        WHERE Id = @UserId
        """;

    public const string FindRoutableUsersWithEmbedding = """
        SELECT picked.Id,
               picked.Name,
               picked.Email,
               picked.VecSim,
               COALESCE(topics.TopicsJson, '[]') AS TopicsJson
        FROM (
            SELECT u.Id,
                   u.Name,
                   u.Email,
                   1 - (u.ExpertiseEmbedding <=> @Vector) AS VecSim
            FROM Users u
            WHERE u.AllowRouting = true
              AND u.ExpertiseEmbedding IS NOT NULL
            ORDER BY u.ExpertiseEmbedding <=> @Vector
            LIMIT @Limit
        ) AS picked
        LEFT JOIN LATERAL (
            SELECT COALESCE(json_agg(top_topics.Name ORDER BY top_topics.Strength DESC, top_topics.Name)::text, '[]') AS TopicsJson
            FROM (
                SELECT t.Name, ute.Strength
                FROM UserTopicExpertise ute
                JOIN Topics t ON t.Id = ute.TopicId
                WHERE ute.UserId = picked.Id
                  AND ute.Strength * power(0.5::double precision, GREATEST(EXTRACT(EPOCH FROM (now() - COALESCE(ute.LastUpdated, now()))), 0) / @HalfLifeSeconds) >= @MinimumStrength
                ORDER BY ute.Strength * power(0.5::double precision, GREATEST(EXTRACT(EPOCH FROM (now() - COALESCE(ute.LastUpdated, now()))), 0) / @HalfLifeSeconds) DESC, t.Name
                LIMIT 20
            ) AS top_topics
        ) AS topics ON true
        ORDER BY picked.VecSim DESC
        """;
}
