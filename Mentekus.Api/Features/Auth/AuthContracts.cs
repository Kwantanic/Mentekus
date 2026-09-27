namespace Mentekus.Api.Features.Auth;

public sealed record AuthUser(Guid Id, string Name, string Email);

public sealed record AuthSessionResponse(Guid Id, string Name, string Email, string XsrfToken);

public sealed record XsrfTokenResponse(string Token);

public sealed record AuthSignedOut();
