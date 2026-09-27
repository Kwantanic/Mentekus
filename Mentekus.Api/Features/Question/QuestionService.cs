using System.Data;
using Dapper;
using Mentekus.Api.Features.Expertise;
using Mentekus.Api.Features.Question.Entities;
using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Mentekus.Api.Shared.Adapters;
using Pgvector;

namespace Mentekus.Api.Features.Question;

[RegisterScoped(ServiceType = typeof(IQuestionService))]
public class QuestionService(
    IOllamaAdapter ollamaAdapter,
    IExpertiseService expertiseService,
    IDbConnection connection) : IQuestionService
{
    public async Task<string> AskAsync(string question, Guid userId,
        CancellationToken cancellationToken = default)
    {
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
        await expertiseService.UpdateFromContributionAsync(userId, embedding, question,
            ExpertiseSql.QuestionSourceType, cancellationToken: cancellationToken);

        return $"Question saved (ID: {questionEntity.Id}). Embedding length: {embedding.Length}.";
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

    public async Task<string> AnswerAsync(Guid questionId, string answer, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var exists = await connection.ExecuteScalarAsync<bool>(
            QuestionSql.QuestionExists,
            new { Id = questionId });

        if (!exists)
            throw new NotFoundException($"Question with ID {questionId} not found.");

        var embedding = await ollamaAdapter.EmbedAsync(answer, cancellationToken);
        if (embedding == null)
            throw new EmbeddingFailedException("Failed to generate embedding for the answer.");

        // Always updates vector (generate for topics may fail non-fatally inside)
        await expertiseService.UpdateFromContributionAsync(userId, embedding, answer,
            ExpertiseSql.AnswerSourceType, cancellationToken: cancellationToken);

        return $"Answer recorded for question {questionId}. Expertise updated.";
    }

    internal record QuestionParameter(Guid Id);
}