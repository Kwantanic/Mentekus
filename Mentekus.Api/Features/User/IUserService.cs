using Mentekus.Api.Features.User.Entities;

namespace Mentekus.Api.Features.User;

public interface IUserService
{
    Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserAccess?> GetUserAccessByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserPreferences> UpdateUserPreferencesAsync(Guid userId, bool? profileVisible = null, bool? allowRouting = null, CancellationToken cancellationToken = default);
}