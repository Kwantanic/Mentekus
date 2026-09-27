using System.Data;
using Dapper;
using Mentekus.Api.Features.Auth.Entities;
using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Npgsql;

namespace Mentekus.Api.Features.Auth;

[RegisterScoped(ServiceType = typeof(IAuthService))]
public class AuthService(IDbConnection connection) : IAuthService
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;

    public async Task<AuthUser> RegisterAsync(string name, string email, string password, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        email = email.Trim();
        ValidateRegistration(name, email, password);

        var existing = await connection.ExecuteScalarAsync<Guid?>(AuthSql.GetUserIdByEmail, new { Email = email });
        if (existing != null)
            throw new ValidationException("Email", "User with this email already exists.");

        var id = Guid.NewGuid();
        try
        {
            await connection.ExecuteAsync(AuthSql.InsertUser, new
            {
                Id = id,
                Name = name,
                Email = email,
                PasswordHash = PasswordHasher.Hash(password)
            });
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ValidationException("Email", "User with this email already exists.");
        }

        return new AuthUser(id, name, email);
    }

    public async Task<AuthUser> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        email = email.Trim();
        var row = await connection.QuerySingleOrDefaultAsync<AuthCredentialRow>(
            AuthSql.FindCredentialByEmail, new { Email = email });

        var matches = PasswordHasher.Verify(password, row?.PasswordHash);
        if (row?.PasswordHash == null || !matches)
            throw new UnauthorizedAccessException("Invalid email or password.");

        return new AuthUser(row.Id, row.Name, row.Email);
    }

    public async Task<AuthUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var row = await connection.QuerySingleOrDefaultAsync<AuthUserRow>(AuthSql.FindUserById, new { Id = userId });
        return row == null ? null : new AuthUser(row.Id, row.Name, row.Email);
    }

    private static void ValidateRegistration(string name, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Name", "Name is required.");
        if (name.Length > 200)
            throw new ValidationException("Name", "Name must be 200 characters or fewer.");
        if (string.IsNullOrWhiteSpace(email))
            throw new ValidationException("Email", "Email is required.");
        if (email.Length > 320)
            throw new ValidationException("Email", "Email must be 320 characters or fewer.");
        if (password.Length is < MinPasswordLength or > MaxPasswordLength)
            throw new ValidationException("Password", $"Password must be between {MinPasswordLength} and {MaxPasswordLength} characters.");
    }
}
