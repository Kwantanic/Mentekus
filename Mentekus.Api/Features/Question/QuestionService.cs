using System.Data;
using Dapper;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Features.User;
using Mentekus.Api.Shared.Adapters;
using Pgvector;

namespace Mentekus.Api.Features.Question;

[RegisterScoped(ServiceType = typeof(IQuestionService))]
public class QuestionService(
    IOllamaAdapter ollamaAdapter,
    IUserService userService,
    IDbConnection connection) : IQuestionService
{
    public async Task<string> AskAsync(string question, string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required to ask a question.");

        var userId = await userService.GetUserIdByEmailAsync(email, cancellationToken);
        if (userId == null)
            throw new InvalidOperationException($"User with email {email} not found.");

        var embedding = await ollamaAdapter.EmbedAsync(question, cancellationToken);

        var questionEntity = new Entities.Question
        {
            Id = Guid.NewGuid(),
            Text = question,
            Embedding = embedding != null ? new Vector(embedding) : null,
            CreatedAt = DateTime.UtcNow,
            AskedByUserId = userId
        };

        await connection.ExecuteAsync(QuestionSql.InsertQuestion, questionEntity);

        return $"Question saved (ID: {questionEntity.Id}). Embedding length: {embedding?.Length ?? 0}.";
    }

    public async Task<List<QuestionSimilarityResponse>> GetSimilarQuestionsAsync(string text, int limit,
        CancellationToken cancellationToken = default)
    {
        var embedding = await ollamaAdapter.EmbedAsync(text, cancellationToken);

        if (embedding == null) return [];

        var vector = new Vector(embedding);

        var result =
            await connection.QueryAsync<QuestionSimilarityResponse>(QuestionSql.FindSimilarQuestions,
                new { Vector = vector, Limit = limit });
        return result.ToList();
    }
}