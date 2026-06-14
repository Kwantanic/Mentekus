namespace Mentekus.Api.Features.User;

public static class UserSql
{
    public const string GetUserIdByEmail = """
        SELECT Id 
        FROM Users 
        WHERE LOWER(Email) = LOWER(@Email)
        """;

    public const string InsertUser = """
        INSERT INTO Users (Id, Name, Email) 
        VALUES (@Id, @Name, @Email)
        """;

    public const string GetUserByEmail = """
        SELECT Id, Name, Email, ProfileVisible, AllowRouting, ExpertiseEmbedding, ExpertiseSummary, LastExpertiseUpdate, CreatedAt, ExternalId
        FROM Users 
        WHERE LOWER(Email) = LOWER(@Email)
        """;

    public const string UpdateUserPreferences = """
        UPDATE Users 
        SET 
            ProfileVisible = COALESCE(@ProfileVisible, ProfileVisible),
            AllowRouting = COALESCE(@AllowRouting, AllowRouting)
        WHERE Id = @UserId
        """;

    public const string FindRoutableUsersWithEmbedding = """
        SELECT Id, Name, Email, ExpertiseEmbedding, ProfileVisible, AllowRouting
        FROM Users 
        WHERE AllowRouting = true AND ExpertiseEmbedding IS NOT NULL
        ORDER BY ExpertiseEmbedding <=> @Vector
        LIMIT @Limit
        """;
}