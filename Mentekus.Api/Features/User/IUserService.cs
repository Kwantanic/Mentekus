namespace Mentekus.Api.Features.User;

public interface IUserService
{
    Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Guid> AddUserAsync(string name, string email, CancellationToken cancellationToken = default);
    Task<User.Entities.User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task UpdateUserPreferencesAsync(Guid userId, bool? profileVisible = null, bool? allowRouting = null, CancellationToken cancellationToken = default);
}