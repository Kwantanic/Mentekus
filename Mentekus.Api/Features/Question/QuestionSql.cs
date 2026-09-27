namespace Mentekus.Api.Features.Question;

public static class QuestionSql
{
    public const string InsertQuestion = """
        INSERT INTO Questions (Id, Text, Embedding, CreatedAt, AskedByUserId) 
        VALUES (@Id, @Text, @Embedding, @CreatedAt, @AskedByUserId)
        """;

    public const string InsertAnswer = """
        INSERT INTO Answers (Id, QuestionId, Text, Embedding, CreatedAt, AnsweredByUserId)
        VALUES (@Id, @QuestionId, @Text, @Embedding, @CreatedAt, @AnsweredByUserId)
        """;

    public const string FindSimilarQuestions = """
        SELECT q.Id AS QuestionId,
               q.Text,
               1 - (q.Embedding <=> @Vector) AS Similarity,
               CASE WHEN u.ProfileVisible OR u.Id = @CallerId THEN q.AskedByUserId ELSE NULL END AS AskedByUserId,
               CASE WHEN u.ProfileVisible OR u.Id = @CallerId THEN u.Email ELSE NULL END AS AskedByEmail
        FROM Questions q
        JOIN Users u ON q.AskedByUserId = u.Id
        WHERE q.Embedding IS NOT NULL
        ORDER BY q.Embedding <=> @Vector
        LIMIT @Limit
        """;

    public const string QuestionExists = """
        SELECT EXISTS(SELECT 1 FROM Questions WHERE Id = @Id)
        """;

    public const string FindQuestion = """
        SELECT q.Id,
               q.Text,
               q.CreatedAt,
               CASE WHEN u.Id IS NOT NULL AND (u.ProfileVisible OR u.Id = @CallerId) THEN q.AskedByUserId ELSE NULL END AS AskedByUserId,
               CASE WHEN u.Id IS NOT NULL AND (u.ProfileVisible OR u.Id = @CallerId) THEN u.Email ELSE NULL END AS AskedByEmail
        FROM Questions q
        LEFT JOIN Users u ON u.Id = q.AskedByUserId
        WHERE q.Id = @Id
        """;

    public const string FindAnswers = """
        SELECT a.Id,
               a.Text,
               a.CreatedAt,
               CASE WHEN u.ProfileVisible OR u.Id = @CallerId THEN a.AnsweredByUserId ELSE NULL END AS AnsweredByUserId,
               CASE WHEN u.ProfileVisible OR u.Id = @CallerId THEN u.Email ELSE NULL END AS AnsweredByEmail
        FROM Answers a
        JOIN Users u ON u.Id = a.AnsweredByUserId
        WHERE a.QuestionId = @QuestionId
        ORDER BY a.CreatedAt, a.Id
        """;
}
