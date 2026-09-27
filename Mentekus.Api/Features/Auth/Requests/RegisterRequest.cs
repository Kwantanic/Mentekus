using System.Text.Json.Serialization;

namespace Mentekus.Api.Features.Auth.Requests;

public sealed record RegisterRequest(
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Email,
    [property: JsonRequired] string Password);
