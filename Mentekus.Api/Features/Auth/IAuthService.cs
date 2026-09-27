namespace Mentekus.Api.Features.Auth;

public interface IAuthService
{
    Task<AuthUser> RegisterAsync(string name, string email, string password, CancellationToken cancellationToken = default);

    Task<AuthUser> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<AuthUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
