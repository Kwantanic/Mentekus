using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Generated;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mentekus.Api.Features.Question;

[EndpointGroup]
public static class QuestionEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("question/").WithTags("Question");

        group.MapPost("ask", HandleAskAsync);
        group.MapPost("similarity", HandleSimilarityAsync);
    }

    private static async Task<Ok<string>> HandleAskAsync(QuestionAskRequest request,
        IQuestionService questionService,
        CancellationToken cancellationToken)
    {
        var answer = await questionService.AskAsync(request.Question, request.Email, cancellationToken);
        return TypedResults.Ok(answer);
    }

    private static async Task<Ok<List<QuestionSimilarityResponse>>> HandleSimilarityAsync(
        QuestionSimilarityRequest request,
        IQuestionService questionService, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit <= 0 ? 5 : request.Limit, 1, 50);
        var similarQuestions = await questionService.GetSimilarQuestionsAsync(request.Text, limit, cancellationToken);

        return TypedResults.Ok(similarQuestions);
    }
}