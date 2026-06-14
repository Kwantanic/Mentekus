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

    private static async Task<Results<Ok<string>, BadRequest<string>>> HandleAskAsync(QuestionAskRequest request,
        IQuestionService questionService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question)) return TypedResults.BadRequest("Question is required.");
        if (string.IsNullOrWhiteSpace(request.Email))
            return TypedResults.BadRequest("Email is required.");

        try
        {
            var answer = await questionService.AskAsync(request.Question, request.Email, cancellationToken);
            return TypedResults.Ok(answer);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok<List<QuestionSimilarityResponse>>, BadRequest<string>>> HandleSimilarityAsync(
        QuestionSimilarityRequest request,
        IQuestionService questionService, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text)) return TypedResults.BadRequest("Text is required.");

        var limit = request.Limit <= 0 ? 5 : request.Limit;
        var similarQuestions = await questionService.GetSimilarQuestionsAsync(request.Text, limit, cancellationToken);

        return TypedResults.Ok(similarQuestions);
    }
}