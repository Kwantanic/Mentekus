using System.Text.Json.Serialization;

namespace Mentekus.Api.Features.Expertise.Requests;

public sealed record ExpertiseRouteRequest(
    [property: JsonRequired] string Query,
    int Limit = 10);
