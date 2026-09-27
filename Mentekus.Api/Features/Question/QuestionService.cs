using Dapper;
using Mentekus.Api.Features.Expertise;
using Mentekus.Api.Features.Question.Entities;
using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Mentekus.Api.Shared;
using Mentekus.Api.Shared.Adapters;
using Npgsql;
using Pgvector;

namespace Mentekus.Api.Features.Question;

[RegisterScoped(ServiceType = typeof(IQuestionService))]
public class QuestionService(
    IOllamaAdapter ollamaAdapter,
    IExpertiseService expertiseService,
    NpgsqlDataSource dataSource) : IQuestionService
{
    public async Task<QuestionCreated> AskAsync(string question, Guid userId, CancellationToken cancellationToken = default)
    {
        var embedding = await ollamaAdapter.EmbedAsync(question, cancellationToken);
        if (embedding == null || embedding.Length == 0)
            throw new EmbeddingFailedException("Failed to generate embedding for the question.");

        EmbeddingSize.Require(embedding);
        var questionEntity = new Entities.Question(
            Guid.NewGuid(),
            question,
            new Vector(embedding),
            DateTime.UtcNow,
            userId);

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await expertiseService.BlendContributionAsync(
                connection, transaction, userId, embedding, ExpertiseSql.QuestionSourceType, cancellationToken: cancellationToken);
            await connection.ExecuteAsync(QuestionSql.InsertQuestion, questionEntity, transaction);
            await transaction.CommitAsync(cancellationToken);
        }

        await expertiseService.EnqueueTopicsAsync(userId, question, ExpertiseSql.QuestionSourceType, cancellationToken);
        return new QuestionCreated(questionEntity.Id, questionEntity.CreatedAt);
    }

    public async Task<QuestionDetail> GetAsync(Guid questionId, Guid callerId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<QuestionDetailRow>(
            QuestionSql.FindQuestion,
            new { Id = questionId, CallerId = callerId });

        if (row == null)
            throw new NotFoundException($"Question with ID {questionId} not found.");

        var answers = (await connection.QueryAsync<QuestionAnswer>(
            QuestionSql.FindAnswers,
            new { QuestionId = questionId, CallerId = callerId })).ToList();

        return new QuestionDetail(row.Id, row.Text, row.CreatedAt, row.AskedByUserId, row.AskedByEmail, answers);
    }

    public async Task<List<QuestionSimilarity>> GetSimilarQuestionsAsync(string text, int limit, Guid callerId, CancellationToken cancellationToken = default)
    {
        limit = ResultLimits.Clamp(limit, 5);
        var embedding = await ollamaAdapter.EmbedAsync(text, cancellationToken);
        if (embedding == null || embedding.Length == 0)
            throw new EmbeddingFailedException("Failed to generate embedding for the similarity search.");

        EmbeddingSize.Require(embedding);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var result = await connection.QueryAsync<QuestionSimilarity>(
            QuestionSql.FindSimilarQuestions,
            new { Vector = new Vector(embedding), Limit = limit, CallerId = callerId });
        return result.ToList();
    }

    public async Task<AnswerCreated> AnswerAsync(Guid questionId, string answer, Guid userId, CancellationToken cancellationToken = default)
    {
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var exists = await connection.ExecuteScalarAsync<bool>(QuestionSql.QuestionExists, new { Id = questionId });
            if (!exists)
                throw new NotFoundException($"Question with ID {questionId} not found.");
        }

        var embedding = await ollamaAdapter.EmbedAsync(answer, cancellationToken);
        if (embedding == null || embedding.Length == 0)
            throw new EmbeddingFailedException("Failed to generate embedding for the answer.");

        EmbeddingSize.Require(embedding);
        var answerEntity = new Answer(
            Guid.NewGuid(),
            questionId,
            answer,
            new Vector(embedding),
            DateTime.UtcNow,
            userId);

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            // The question can be removed while the answer is being embedded.
            var exists = await connection.ExecuteScalarAsync<bool>(
                QuestionSql.QuestionExists,
                new { Id = questionId },
                transaction);

            if (!exists)
                throw new NotFoundException($"Question with ID {questionId} not found.");

            await expertiseService.BlendContributionAsync(
                connection, transaction, userId, embedding, ExpertiseSql.AnswerSourceType, cancellationToken: cancellationToken);
            await connection.ExecuteAsync(QuestionSql.InsertAnswer, answerEntity, transaction);
            await transaction.CommitAsync(cancellationToken);
        }

        await expertiseService.EnqueueTopicsAsync(userId, answer, ExpertiseSql.AnswerSourceType, cancellationToken);
        return new AnswerCreated(answerEntity.Id, questionId, answerEntity.CreatedAt);
    }
}
