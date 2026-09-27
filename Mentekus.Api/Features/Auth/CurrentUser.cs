using System.Security.Claims;

namespace Mentekus.Api.Features.Auth;

public static class CurrentUser
{
    public static Guid GetId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
