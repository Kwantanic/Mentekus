using System.Data;
using Dapper;

namespace Mentekus.Api.Features.User;

[RegisterScoped(ServiceType = typeof(IUserService))]
public class UserService(IDbConnection connection) : IUserService
{
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