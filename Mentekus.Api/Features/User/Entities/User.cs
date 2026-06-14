using Pgvector;

namespace Mentekus.Api.Features.User.Entities;

public record User(
    Guid Id,
    string? ExternalId,
    string Name,
    string Email,
    DateTime CreatedAt,
    bool ProfileVisible,
    bool AllowRouting,
    Vector? ExpertiseEmbedding,
    string? ExpertiseSummary,
    DateTime? LastExpertiseUpdate);