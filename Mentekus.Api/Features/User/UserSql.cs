namespace Mentekus.Api.Features.User;

public static class UserSql
{
    public const string GetUserIdByEmail = """
        SELECT Id
        FROM Users
        WHERE Email = CAST(@Email AS citext)
        """;

    public const string GetUserAccessByEmail = """
        SELECT Id, Email, ProfileVisible
        FROM Users
        WHERE Email = CAST(@Email AS citext)
        """;

    public const string UpdateUserPreferences = """
        UPDATE Users
        SET
            ProfileVisible = COALESCE(@ProfileVisible, ProfileVisible),
            AllowRouting = COALESCE(@AllowRouting, AllowRouting)
        WHERE Id = @UserId
        RETURNING ProfileVisible, AllowRouting
        """;
}