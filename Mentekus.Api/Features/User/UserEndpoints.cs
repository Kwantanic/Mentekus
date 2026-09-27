using System.Security.Claims;
using Mentekus.Api.Features.Auth;
using Mentekus.Api.Features.Expertise;
using Mentekus.Api.Features.Expertise.Entities;
using Mentekus.Api.Features.Expertise.Requests;
using Mentekus.Api.Generated;
using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mentekus.Api.Features.User;

[EndpointGroup]
public static class UserEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("user/").WithTags("User").RequireAuthorization();

        group.MapGet("{email}/expertise", HandleGetExpertiseProfileAsync);
        group.MapPost("{email}/preferences", HandleUpdatePreferencesAsync);
    }

    private static async Task<Ok<UserExpertiseProfile>> HandleGetExpertiseProfileAsync(
        string email,
        ClaimsPrincipal user,
        IUserService userService,
        IExpertiseService expertiseService,
        CancellationToken cancellationToken)
    {
        var access = await userService.GetUserAccessByEmailAsync(email, cancellationToken);
        var callerId = CurrentUser.GetId(user);
        if (access == null || (access.Id != callerId && !access.ProfileVisible))
            throw new NotFoundException($"User with email {email} not found.");

        var profile = await expertiseService.GetUserExpertiseAsync(access.Id, cancellationToken);
        if (profile == null)
            throw new NotFoundException($"User with email {email} not found.");

        return TypedResults.Ok(profile);
    }

    private static async Task<Ok<string>> HandleUpdatePreferencesAsync(
        string email,
        UserPreferencesUpdateRequest request,
        ClaimsPrincipal user,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var access = await userService.GetUserAccessByEmailAsync(email, cancellationToken);
        if (access == null)
            throw new NotFoundException($"User with email {email} not found.");
        if (access.Id != CurrentUser.GetId(user))
            throw new ForbiddenException("You can only update your own preferences.");

        await userService.UpdateUserPreferencesAsync(access.Id, request.ProfileVisible, request.AllowRouting, cancellationToken);
        return TypedResults.Ok("Preferences updated.");
    }
}

[EndpointGroup]
public static class ExpertiseEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("expertise/").WithTags("Expertise").RequireAuthorization();

        group.MapPost("document", HandleIngestDocumentAsync);
        group.MapPost("route", HandleRouteAsync);
    }

    private static async Task<Ok<string>> HandleIngestDocumentAsync(
        ExpertiseDocumentIngestRequest request,
        IExpertiseService expertiseService,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var result = await expertiseService.IngestDocumentAsync(request.Text, CurrentUser.GetId(user), cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<List<ExpertiseRouteMatch>>> HandleRouteAsync(
        ExpertiseRouteRequest request,
        IExpertiseService expertiseService,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit <= 0 ? 10 : request.Limit, 1, 50);
        var matches = await expertiseService.RouteExpertsAsync(request.Query, limit, cancellationToken);
        return TypedResults.Ok(matches);
    }
}

public sealed record UserPreferencesUpdateRequest(
    bool? ProfileVisible = null,
    bool? AllowRouting = null);
