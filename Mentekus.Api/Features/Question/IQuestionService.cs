using Mentekus.Api.Features.Question.Entities;

namespace Mentekus.Api.Features.Question;

public interface IQuestionService
{
    Task<QuestionCreated> AskAsync(string question, Guid userId, CancellationToken cancellationToken = default);

    Task<QuestionDetail> GetAsync(Guid questionId, Guid callerId, CancellationToken cancellationToken = default);

    Task<List<QuestionSimilarity>> GetSimilarQuestionsAsync(string text, int limit, Guid callerId, CancellationToken cancellationToken = default);

    Task<AnswerCreated> AnswerAsync(Guid questionId, string answer, Guid userId, CancellationToken cancellationToken = default);
}
