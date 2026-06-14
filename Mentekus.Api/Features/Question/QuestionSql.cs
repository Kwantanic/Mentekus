namespace Mentekus.Api.Features.Question;

public static class QuestionSql
{
    public const string InsertQuestion = """
        INSERT INTO Questions (Id, Text, Embedding, CreatedAt, AskedByUserId) 
        VALUES (@Id, @Text, @Embedding, @CreatedAt, @AskedByUserId)
        """;

    public const string FindSimilarQuestions = """
        SELECT q.Text, 1 - (q.Embedding <=> @Vector) AS Similarity, q.AskedByUserId, u.Email AS AskedByEmail
        FROM Questions q
        JOIN Users u ON q.AskedByUserId = u.Id
        WHERE q.Embedding IS NOT NULL
        ORDER BY q.Embedding <=> @Vector
        LIMIT @Limit
        """;
}