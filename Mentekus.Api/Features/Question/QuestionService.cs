using System.Data;
using Dapper;
using Mentekus.Api.Features.Expertise;
using Mentekus.Api.Features.Question.Entities;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Features.User;
using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Mentekus.Api.Shared.Adapters;
using Pgvector;

namespace Mentekus.Api.Features.Question;

[RegisterScoped(ServiceType = typeof(IQuestionService))]
public class QuestionService(
    IOllamaAdapter ollamaAdapter,
    IUserService userService,
    IExpertiseService expertiseService,
    IDbConnection connection) : IQuestionService
{
    public async Task<string> AskAsync(string question, string email,
        CancellationToken cancellationToken = default)
    {
        var userId = await userService.GetUserIdByEmailAsync(email, cancellationToken);
        if (userId == null)
            throw new NotFoundException($"User with email {email} not found.");

        var embedding = await ollamaAdapter.EmbedAsync(question, cancellationToken);
        if (embedding == null)
            throw new EmbeddingFailedException("Failed to generate embedding for the question.");

        var questionEntity = new Entities.Question(
            Guid.NewGuid(),
            question,
            new Vector(embedding),
            DateTime.UtcNow,
            userId);

        await connection.ExecuteAsync(QuestionSql.InsertQuestion, questionEntity);

        // Blending side effect (post-ask): update asker's expertise (small alpha); non-fatal on generate per design
        await expertiseService.UpdateFromContributionAsync(userId.Value, embedding, question, ExpertiseSql.QuestionSourceType, cancellationToken: cancellationToken);

        return $"Question saved (ID: {questionEntity.Id}). Embedding length: {embedding?.Length ?? 0}.";
    }

    public async Task<List<QuestionSimilarity>> GetSimilarQuestionsAsync(string text, int limit,
        CancellationToken cancellationToken = default)
    {
        var embedding = await ollamaAdapter.EmbedAsync(text, cancellationToken);

        if (embedding == null) return [];

        var vector = new Vector(embedding);

        var result =
            await connection.QueryAsync<QuestionSimilarity>(QuestionSql.FindSimilarQuestions,
                new { Vector = vector, Limit = limit });
        return result.ToList();
    }

    public async Task<string> AnswerAsync(Guid questionId, string answer, string email, CancellationToken cancellationToken = default)
    {
        // CT via CommandDefinition for new answer path (matches the addressed pattern in new Expertise internal; other Dapper calls follow pre-existing no-CT style in this file).
        var existsCmd = new CommandDefinition(QuestionSql.QuestionExists, new { Id = questionId }, cancellationToken: cancellationToken);
        var exists = await connection.ExecuteScalarAsync<bool>(existsCmd);
        if (!exists)
            throw new NotFoundException($"Question with ID {questionId} not found.");

        var userId = await userService.GetUserIdByEmailAsync(email, cancellationToken);
        if (userId == null)
            throw new NotFoundException($"User with email {email} not found.");

        var embedding = await ollamaAdapter.EmbedAsync(answer, cancellationToken);
        if (embedding == null)
            throw new EmbeddingFailedException("Failed to generate embedding for the answer.");

        // Always updates vector (generate for topics may fail non-fatally inside)
        await expertiseService.UpdateFromContributionAsync(userId.Value, embedding, answer, ExpertiseSql.AnswerSourceType, cancellationToken: cancellationToken);

        return $"Answer recorded for question {questionId}. Expertise updated.";
    }
}