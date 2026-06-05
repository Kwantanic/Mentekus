namespace Mentekus.Api.Features.Question;

public static class QuestionSql
{
    public const string InsertQuestion =
        "INSERT INTO Questions (Id, Text, Embedding, CreatedAt, AskedByUserId) VALUES (@Id, @Text, @Embedding, @CreatedAt, @AskedByUserId)";

    public const string FindSimilarQuestions = """
                                               SELECT Text, 1 - (Embedding <=> @Vector) AS Similarity
                                               FROM Questions
                                               WHERE Embedding IS NOT NULL
                                               ORDER BY Embedding <=> @Vector
                                               LIMIT @Limit
                                               """;
}