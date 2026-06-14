using Mentekus.Api.Features.Question.Entities;
using Mentekus.Api.Features.Question.Requests;

namespace Mentekus.Api.Features.Question;

public interface IQuestionService
{
    Task<string> AskAsync(string question, string email,
        CancellationToken cancellationToken = default);

    Task<List<QuestionSimilarity>> GetSimilarQuestionsAsync(string text, int limit,
        CancellationToken cancellationToken = default);

    Task<string> AnswerAsync(Guid questionId, string answer, string email, CancellationToken cancellationToken = default);
}