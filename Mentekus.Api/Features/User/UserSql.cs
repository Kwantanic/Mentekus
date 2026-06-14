namespace Mentekus.Api.Features.User;

public static class UserSql
{
    public const string GetUserIdByEmail = """
        SELECT Id 
        FROM Users 
        WHERE Email = @Email
        """;

    public const string InsertUser = """
        INSERT INTO Users (Id, Name, Email) 
        VALUES (@Id, @Name, @Email)
        """;
}