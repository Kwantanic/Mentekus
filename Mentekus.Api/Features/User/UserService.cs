using System.Data;
using Dapper;

namespace Mentekus.Api.Features.User;

[RegisterScoped(ServiceType = typeof(IUserService))]
public class UserService(IDbConnection connection) : IUserService
{
    public async Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await connection.ExecuteScalarAsync<Guid?>(
            UserSql.GetUserIdByEmail,
            new { Email = email });
    }

    public async Task<Guid> AddUserAsync(string name, string email, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            UserSql.InsertUser,
            new { Id = id, Name = name, Email = email });
        return id;
    }

    public async Task UpdateUserPreferencesAsync(Guid userId, bool? profileVisible = null, bool? allowRouting = null, CancellationToken cancellationToken = default)
    {
        await connection.ExecuteAsync(UserSql.UpdateUserPreferences, new { UserId = userId, ProfileVisible = profileVisible, AllowRouting = allowRouting });
    }
}