namespace Mentekus.Api.Features.Expertise;

public interface IExpertiseService
{
    Task UpdateVectorOnlyFromContributionAsync(Guid userId, float[] embedding, string sourceType, float? alphaOverride = null, CancellationToken cancellationToken = default);

    Task UpdateFromContributionAsync(Guid userId, float[] embedding, string text, string sourceType, float? alphaOverride = null, CancellationToken cancellationToken = default);

    Task<string[]> ExtractTopicsAsync(string text, string? sourceType = null, CancellationToken cancellationToken = default);

    Task<UserExpertiseProfile?> GetUserExpertiseAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<string> IngestDocumentAsync(string text, string email, CancellationToken cancellationToken = default);

    Task<List<ExpertiseRouteMatch>> RouteExpertsAsync(string query, int limit, CancellationToken cancellationToken = default);
}

public sealed record UserExpertiseProfile(
    Guid UserId,
    string Name,
    string Email,
    string? ExpertiseSummary,
    string[] TopTopics,
    DateTime? LastUpdated);

public sealed record ExpertiseDocumentIngestRequest(
    [property: System.Text.Json.Serialization.JsonRequired] string Text,
    [property: System.Text.Json.Serialization.JsonRequired] string Email);

public sealed record ExpertiseRouteRequest(
    [property: System.Text.Json.Serialization.JsonRequired] string Query,
    int Limit = 10);

public sealed record ExpertiseRouteMatch(
    Guid UserId,
    string Name,
    string Email,
    double Score,
    double VecSim,
    string[] MatchedTopics,
    double Confidence);