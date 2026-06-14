using Mentekus.Api.Features.User.Requests;
using Mentekus.Api.Generated;
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

    private static async Task<Results<Ok<UserAddResponse>, BadRequest<string>>> HandleAddAsync(
        UserAddRequest request,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return TypedResults.BadRequest("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Email)) return TypedResults.BadRequest("Email is required.");

        var existingUserId = await userService.GetUserIdByEmailAsync(request.Email, cancellationToken);
        if (existingUserId != null)
        {
            return TypedResults.BadRequest("User with this email already exists.");
        }

        var id = await userService.AddUserAsync(request.Name, request.Email, cancellationToken);

        return TypedResults.Ok(new UserAddResponse(id, request.Name, request.Email));
    }
}
