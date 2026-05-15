namespace Mentekus.Api.Features.Question;

public static class QuestionEndpoints
{
    public static RouteGroupBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("question/");

        group.MapGet("ask", () => Results.Ok("Hello from question!"));

        return group;
    }
}