namespace Mentekus.Api.Features.User;

public interface IUserService
{
    Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Guid> AddUserAsync(string name, string email, CancellationToken cancellationToken = default);
}