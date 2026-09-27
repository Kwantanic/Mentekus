namespace Mentekus.Api.Features.User;

public interface IUserService
{
    Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserAccess?> GetUserAccessByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Guid> AddUserAsync(string name, string email, CancellationToken cancellationToken = default);
    Task UpdateUserPreferencesAsync(Guid userId, bool? profileVisible = null, bool? allowRouting = null, CancellationToken cancellationToken = default);
}