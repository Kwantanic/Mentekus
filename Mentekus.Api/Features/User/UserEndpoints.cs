using System.Security.Claims;
using Mentekus.Api.Features.Auth;
using Mentekus.Api.Features.Expertise;
using Mentekus.Api.Features.Expertise.Entities;
using Mentekus.Api.Features.User.Entities;
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

    private static async Task<Ok<UserPreferences>> HandleUpdatePreferencesAsync(
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

        var updated = await userService.UpdateUserPreferencesAsync(access.Id, request.ProfileVisible, request.AllowRouting, cancellationToken);
        return TypedResults.Ok(updated);
    }
}
