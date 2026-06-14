using Mentekus.Api.Features.Expertise;
using Mentekus.Api.Features.User.Requests;
using Mentekus.Api.Generated;
using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mentekus.Api.Features.User;

[EndpointGroup]
public static class UserEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("user/").WithTags("User");

        group.MapPost("add", HandleAddAsync);
        group.MapGet("{email}/expertise", HandleGetExpertiseProfileAsync);
        group.MapPost("{email}/preferences", HandleUpdatePreferencesAsync);
    }

    private static async Task<Ok<UserAddResponse>> HandleAddAsync(
        UserAddRequest request,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var existingUserId = await userService.GetUserIdByEmailAsync(request.Email, cancellationToken);
        if (existingUserId != null)
        {
            throw new ValidationException(nameof(request.Email), "User with this email already exists.");
        }

        var id = await userService.AddUserAsync(request.Name, request.Email, cancellationToken);

        return TypedResults.Ok(new UserAddResponse(id, request.Name, request.Email));
    }

    private static async Task<Ok<UserExpertiseProfile>> HandleGetExpertiseProfileAsync(
        string email,
        IUserService userService,
        IExpertiseService expertiseService,
        CancellationToken cancellationToken)
    {
        var userId = await userService.GetUserIdByEmailAsync(email, cancellationToken);
        if (userId == null)
            throw new NotFoundException($"User with email {email} not found.");

        var profile = await expertiseService.GetUserExpertiseAsync(userId.Value, cancellationToken);
        // profile cannot be null here: prior GetUserIdByEmailAsync succeeded so the user row exists (GetUserExpertiseAsync only nulls on missing user row)
        return TypedResults.Ok(profile!);
    }

    private static async Task<Ok<string>> HandleUpdatePreferencesAsync(
        string email,
        UserPreferencesUpdateRequest request,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var userId = await userService.GetUserIdByEmailAsync(email, cancellationToken);
        if (userId == null)
            throw new NotFoundException($"User with email {email} not found.");

        await userService.UpdateUserPreferencesAsync(userId.Value, request.ProfileVisible, request.AllowRouting, cancellationToken);
        return TypedResults.Ok("Preferences updated.");
    }
}

// Expertise endpoints defined in this file (avoids new file per constraints; generator discovers [EndpointGroup] types)
[EndpointGroup]
public static class ExpertiseEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("expertise/").WithTags("Expertise");

        group.MapPost("document", HandleIngestDocumentAsync);
        group.MapPost("route", HandleRouteAsync);
    }

    private static async Task<Ok<string>> HandleIngestDocumentAsync(
        ExpertiseDocumentIngestRequest request,
        IExpertiseService expertiseService,
        CancellationToken cancellationToken)
    {
        var result = await expertiseService.IngestDocumentAsync(request.Text, request.Email, cancellationToken);
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
