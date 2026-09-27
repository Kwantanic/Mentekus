using Dapper;
using Mentekus.Api.Features.User.Entities;
using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Npgsql;

namespace Mentekus.Api.Features.User;

[RegisterScoped(ServiceType = typeof(IUserService))]
public class UserService(NpgsqlDataSource dataSource) : IUserService
{
    public async Task<Guid?> GetUserIdByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Guid?>(
            UserSql.GetUserIdByEmail,
            new { Email = email.Trim() });
    }

    public async Task<UserAccess?> GetUserAccessByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserAccessRow>(
            UserSql.GetUserAccessByEmail,
            new { Email = email.Trim() });

        return row == null ? null : new UserAccess(row.Id, row.Email, row.ProfileVisible);
    }

    public async Task<UserPreferences> UpdateUserPreferencesAsync(Guid userId, bool? profileVisible = null, bool? allowRouting = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var updated = await connection.QuerySingleOrDefaultAsync<UserPreferences>(
            UserSql.UpdateUserPreferences,
            new { UserId = userId, ProfileVisible = profileVisible, AllowRouting = allowRouting });
        if (updated == null)
            throw new NotFoundException("User not found.");
        return updated;
    }
}
