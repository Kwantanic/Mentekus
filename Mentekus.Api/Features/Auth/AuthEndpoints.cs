using System.Security.Claims;
using Mentekus.Api.Features.Auth.Requests;
using Mentekus.Api.Generated;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mentekus.Api.Features.Auth;

[EndpointGroup]
public static class AuthEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("auth/").WithTags("Auth");

        group.MapGet("xsrf", HandleXsrf);
        group.MapPost("register", HandleRegisterAsync);
        group.MapPost("login", HandleLoginAsync);
        group.MapPost("logout", (Delegate)HandleLogoutAsync).RequireAuthorization();
        group.MapGet("me", HandleMeAsync).RequireAuthorization();
    }

    private static Ok<XsrfTokenResponse> HandleXsrf(HttpContext httpContext, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        if (string.IsNullOrEmpty(tokens.RequestToken))
            throw new InvalidOperationException("XSRF token could not be issued.");

        return TypedResults.Ok(new XsrfTokenResponse(tokens.RequestToken));
    }

    private static async Task<Ok<AuthSessionResponse>> HandleRegisterAsync(
        RegisterRequest request,
        IAuthService authService,
        IAntiforgery antiforgery,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await authService.RegisterAsync(request.Name, request.Email, request.Password, cancellationToken);
        await SignInAsync(httpContext, user);
        return TypedResults.Ok(new AuthSessionResponse(user.Id, user.Name, user.Email, IssueXsrfToken(httpContext, antiforgery)));
    }

    private static async Task<Ok<AuthSessionResponse>> HandleLoginAsync(
        LoginRequest request,
        IAuthService authService,
        IAntiforgery antiforgery,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await authService.LoginAsync(request.Email, request.Password, cancellationToken);
        await SignInAsync(httpContext, user);
        return TypedResults.Ok(new AuthSessionResponse(user.Id, user.Name, user.Email, IssueXsrfToken(httpContext, antiforgery)));
    }

    private static async Task<Ok<AuthSignedOut>> HandleLogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.Ok(new AuthSignedOut());
    }

    private static async Task<Ok<AuthUser>> HandleMeAsync(
        IAuthService authService,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var current = await authService.GetUserAsync(CurrentUser.GetId(user), cancellationToken);
        if (current == null)
            throw new UnauthorizedAccessException("Authentication is required.");

        return TypedResults.Ok(current);
    }

    private static async Task SignInAsync(HttpContext httpContext, AuthUser user)
    {
        var claims = new Claim[]
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        // Sign-in writes the cookie for the next request. The XSRF token issued in this response
        // must be bound to the same principal or later calls are rejected as a different user.
        httpContext.User = principal;
    }

    private static string IssueXsrfToken(HttpContext httpContext, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        if (string.IsNullOrEmpty(tokens.RequestToken))
            throw new InvalidOperationException("XSRF token could not be issued.");

        return tokens.RequestToken;
    }
}
