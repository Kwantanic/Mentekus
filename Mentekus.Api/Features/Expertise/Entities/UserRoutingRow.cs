namespace Mentekus.Api.Features.Expertise.Entities;

internal record UserRoutingRow(
    Guid Id,
    string Name,
    string Email,
    double VecSim,
    string TopicsJson);
