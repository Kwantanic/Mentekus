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

    public async Task<Guid?> ResolveOrCreateUserAsync(string? name, string? email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var userId = await connection.ExecuteScalarAsync<Guid?>(
            UserSql.GetUserIdByEmail,
            new { Email = email });

        if (userId == null)
        {
            userId = Guid.NewGuid();
            await connection.ExecuteAsync(
                UserSql.InsertUser,
                new { Id = userId, Name = name ?? string.Empty, Email = email });
        }

        return userId;
    }
}