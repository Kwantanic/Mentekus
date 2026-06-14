using Mentekus.Api.Features.User.Requests;
using Mentekus.Api.Generated;
using Mentekus.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mentekus.Api.Features.User;

[EndpointGroup]
public static class UserEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("user/").WithTags("User");

        group.MapPost("add", HandleAddAsync);
    }

    private static async Task<Ok<UserAddResponse>> HandleAddAsync(
        UserAddRequest request,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ValidationException(nameof(request.Name), "Name is required.");
        if (string.IsNullOrWhiteSpace(request.Email)) throw new ValidationException(nameof(request.Email), "Email is required.");

        var existingUserId = await userService.GetUserIdByEmailAsync(request.Email, cancellationToken);
        if (existingUserId != null)
        {
            throw new ValidationException(nameof(request.Email), "User with this email already exists.");
        }

        var id = await userService.AddUserAsync(request.Name, request.Email, cancellationToken);

        return TypedResults.Ok(new UserAddResponse(id, request.Name, request.Email));
    }
}
