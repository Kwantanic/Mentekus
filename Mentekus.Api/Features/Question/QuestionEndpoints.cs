using System.Security.Claims;
using Mentekus.Api.Features.Auth;
using Mentekus.Api.Features.Question.Entities;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Generated;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mentekus.Api.Features.Question;

[EndpointGroup]
public static class QuestionEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("question/").WithTags("Question").RequireAuthorization();

        group.MapPost("ask", HandleAskAsync);
        group.MapPost("similarity", HandleSimilarityAsync);
        group.MapPost("answer", HandleAnswerAsync);
    }

    private static async Task<Ok<string>> HandleAskAsync(QuestionAskRequest request,
        IQuestionService questionService,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var answer = await questionService.AskAsync(request.Question, CurrentUser.GetId(user), cancellationToken);
        return TypedResults.Ok(answer);
    }

    private static async Task<Ok<List<QuestionSimilarity>>> HandleSimilarityAsync(
        QuestionSimilarityRequest request,
        IQuestionService questionService, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit <= 0 ? 5 : request.Limit, 1, 50);
        var similarQuestions = await questionService.GetSimilarQuestionsAsync(request.Text, limit, cancellationToken);

        return TypedResults.Ok(similarQuestions);
    }

    private static async Task<Ok<string>> HandleAnswerAsync(
        QuestionAnswerRequest request,
        IQuestionService questionService,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var result = await questionService.AnswerAsync(request.QuestionId, request.Answer, CurrentUser.GetId(user), cancellationToken);
        return TypedResults.Ok(result);
    }
}