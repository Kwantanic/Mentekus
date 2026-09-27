namespace Mentekus.Api.Features.Auth.Entities;

internal record AuthCredentialRow(Guid Id, string Name, string Email, string? PasswordHash);

internal record AuthUserRow(Guid Id, string Name, string Email);
