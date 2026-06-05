namespace Mentekus.Api.Features.User;

public interface IUserService
{
    Task<Guid?> ResolveOrCreateUserAsync(string? name, string? email, CancellationToken cancellationToken = default);
}