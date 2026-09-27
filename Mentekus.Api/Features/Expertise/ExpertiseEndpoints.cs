using System.Security.Claims;
using Mentekus.Api.Features.Auth;
using Mentekus.Api.Features.Expertise.Entities;
using Mentekus.Api.Features.Expertise.Requests;
using Mentekus.Api.Generated;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mentekus.Api.Features.Expertise;

[EndpointGroup]
public static class ExpertiseEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("expertise/").WithTags("Expertise").RequireAuthorization();

        group.MapPost("document", HandleIngestDocumentAsync);
        group.MapPost("route", HandleRouteAsync);
    }

    private static async Task<Ok<UserExpertiseProfile>> HandleIngestDocumentAsync(
        ExpertiseDocumentIngestRequest request,
        IExpertiseService expertiseService,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var profile = await expertiseService.IngestDocumentAsync(request.Text, CurrentUser.GetId(user), cancellationToken);
        return TypedResults.Ok(profile);
    }

    private static async Task<Ok<List<ExpertiseRouteMatch>>> HandleRouteAsync(
        ExpertiseRouteRequest request,
        IExpertiseService expertiseService,
        CancellationToken cancellationToken)
    {
        var matches = await expertiseService.RouteExpertsAsync(request.Query, request.Limit, cancellationToken);
        return TypedResults.Ok(matches);
    }
}
