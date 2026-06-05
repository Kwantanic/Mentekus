using System.Data;
using Dapper;

namespace Mentekus.Api.Features.User;

public class UserService(IDbConnection connection) : IUserService
{
    public async Task<Guid?> ResolveOrCreateUserAsync(string? name, string? email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var userId = await connection.ExecuteScalarAsync<Guid?>(
            "SELECT Id FROM Users WHERE Email = @Email",
            new { Email = email });

        if (userId == null)
        {
            userId = Guid.NewGuid();
            await connection.ExecuteAsync(
                "INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
                new { Id = userId, Name = name ?? string.Empty, Email = email });
        }

        return userId;
    }
}