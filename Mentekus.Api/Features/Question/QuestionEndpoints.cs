using Mentekus.Api.Features.Question.Requests;

namespace Mentekus.Api.Features.Question;

public static class QuestionEndpoints
{
    public static RouteGroupBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("question/");

        group.MapPost("ask", async (
            QuestionAskRequest request,
            IQuestionService questionService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question)) return Results.BadRequest("Question is required.");

            var answer = await questionService.AskAsync(request.Question, cancellationToken);

            return Results.Ok(answer);
        });

        group.MapPost("similarity", async (
            QuestionSimilarityRequest request,
            IQuestionService questionService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text)) return Results.BadRequest("Text is required.");

            var limit = request.Limit <= 0 ? 5 : request.Limit;
            var similarQuestions = await questionService.GetSimilarQuestionsAsync(request.Text, limit, cancellationToken);

            return Results.Ok(similarQuestions);
        });

        return group;
    }
}