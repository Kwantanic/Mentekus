using Mentekus.Api.Features.Expertise.Entities;

namespace Mentekus.Api.Features.Expertise;

public interface IExpertiseService
{
    Task UpdateVectorOnlyFromContributionAsync(Guid userId, float[] embedding, string sourceType, float? alphaOverride = null, CancellationToken cancellationToken = default);

    Task UpdateFromContributionAsync(Guid userId, float[] embedding, string text, string sourceType, float? alphaOverride = null, CancellationToken cancellationToken = default);

    Task<string[]> ExtractTopicsAsync(string text, string? sourceType = null, CancellationToken cancellationToken = default);

    Task<UserExpertiseProfile?> GetUserExpertiseAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<string> IngestDocumentAsync(string text, Guid userId, CancellationToken cancellationToken = default);

    Task<List<ExpertiseRouteMatch>> RouteExpertsAsync(string query, int limit, CancellationToken cancellationToken = default);
}