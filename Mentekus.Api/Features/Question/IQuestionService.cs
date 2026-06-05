using Mentekus.Api.Features.Question.Requests;

namespace Mentekus.Api.Features.Question;

public interface IQuestionService
{
    Task<string> AskAsync(string question, string name, string email,
        CancellationToken cancellationToken = default);

    Task<List<QuestionSimilarityResponse>> GetSimilarQuestionsAsync(string text, int limit,
        CancellationToken cancellationToken = default);
}