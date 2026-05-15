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

        return group;
    }
}