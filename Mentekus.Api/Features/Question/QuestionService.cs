using System.Data;
using Dapper;
using Mentekus.Api.Shared.Adapters;
using Pgvector;

namespace Mentekus.Api.Features.Question;

public interface IQuestionService
{
    Task<string> AskAsync(string question, CancellationToken cancellationToken = default);
}

public class QuestionService(
    IOllamaAdapter ollamaAdapter,
    IConfiguration configuration,
    IDbConnection connection) : IQuestionService
{
    public async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        var model = configuration["Ollama:EmbeddingModel"] ?? "qwen3-embedding:0.6b";

        var request = new OllamaEmbedRequest(
            model,
            question);

        var result = await ollamaAdapter.EmbedAsync(request, cancellationToken);

        var embedding = result?.Embeddings?.FirstOrDefault();
        var embeddingLength = embedding?.Length ?? 0;

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

        return $"You asked: {question}. Embedding length: {embeddingLength}. Saved to DB with ID: {questionEntity.Id}";
    }
}