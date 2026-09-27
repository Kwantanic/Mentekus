using Mentekus.Api.Features.Question.Entities;

namespace Mentekus.Api.Features.Question;

public interface IQuestionService
{
    Task<string> AskAsync(string question, Guid userId,
        CancellationToken cancellationToken = default);

    Task<List<QuestionSimilarity>> GetSimilarQuestionsAsync(string text, int limit,
        CancellationToken cancellationToken = default);

    Task<string> AnswerAsync(Guid questionId, string answer, Guid userId, CancellationToken cancellationToken = default);
}