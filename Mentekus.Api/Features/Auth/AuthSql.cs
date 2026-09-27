namespace Mentekus.Api.Features.Auth;

public static class AuthSql
{
    public const string GetUserIdByEmail = """
        SELECT Id
        FROM Users
        WHERE Email = CAST(@Email AS citext)
        """;

    public const string InsertUser = """
        INSERT INTO Users (Id, Name, Email, PasswordHash)
        VALUES (@Id, @Name, @Email, @PasswordHash)
        """;

    public const string FindCredentialByEmail = """
        SELECT Id, Name, Email, PasswordHash
        FROM Users
        WHERE Email = CAST(@Email AS citext)
        """;

    public const string FindUserById = """
        SELECT Id, Name, Email
        FROM Users
        WHERE Id = @Id
        """;
}
