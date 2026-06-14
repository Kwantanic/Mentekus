namespace Mentekus.Api.Features.Expertise.Entities;

internal record UserExpertiseRow(
    Guid UserId,
    string Name,
    string Email,
    string? ExpertiseSummary,
    DateTime? LastExpertiseUpdate);
