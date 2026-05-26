using System.Data;
using Dapper;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Shared.Adapters;
using Pgvector;

namespace Mentekus.Api.Features.Question;

public interface IQuestionService
{
    Task<string> AskAsync(string question, CancellationToken cancellationToken = default);

    Task<List<QuestionSimilarityResponse>> GetSimilarQuestionsAsync(string text, int limit,
        CancellationToken cancellationToken = default);
}

public class QuestionService(
    IOllamaAdapter ollamaAdapter,
    IDbConnection connection) : IQuestionService
{
    public async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        var embedding = await ollamaAdapter.EmbedAsync(question, cancellationToken);

        var questionEntity = new Entities.Question
        {
            Id = Guid.NewGuid(),
            Text = question,
            Embedding = embedding != null ? new Vector(embedding) : null,
            CreatedAt = DateTime.UtcNow
        };

        const string sql =
            "INSERT INTO Questions (Id, Text, Embedding, CreatedAt) VALUES (@Id, @Text, @Embedding, @CreatedAt)";
        await connection.ExecuteAsync(sql, questionEntity);

        return
            $"You asked: {question}. Embedding length: {embedding?.Length ?? 0}. Saved to DB with ID: {questionEntity.Id}";
    }

    public async Task<List<QuestionSimilarityResponse>> GetSimilarQuestionsAsync(string text, int limit,
        CancellationToken cancellationToken = default)
    {
        var embedding = await ollamaAdapter.EmbedAsync(text, cancellationToken);

        if (embedding == null) return [];

        var vector = new Vector(embedding);

        const string sql = """
                           SELECT Text, 1 - (Embedding <=> @Vector) AS Similarity
                           FROM Questions
                           WHERE Embedding IS NOT NULL
                           ORDER BY Embedding <=> @Vector
                           LIMIT @Limit
                           """;

        var result =
            await connection.QueryAsync<QuestionSimilarityResponse>(sql, new { Vector = vector, Limit = limit });
        return result.ToList();
    }
}