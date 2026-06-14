namespace Mentekus.Api.Features.Expertise.Entities;

public record UserExpertiseProfile(
    Guid UserId,
    string Name,
    string Email,
    string? ExpertiseSummary,
    string[] TopTopics,
    DateTime? LastUpdated);
