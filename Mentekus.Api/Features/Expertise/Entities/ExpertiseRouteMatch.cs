namespace Mentekus.Api.Features.Expertise.Entities;

public record ExpertiseRouteMatch(
    Guid UserId,
    string Name,
    string Email,
    double Score,
    double VecSim,
    string[] MatchedTopics,
    double Confidence);
