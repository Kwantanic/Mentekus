using System.Text.Json.Serialization;

namespace Mentekus.Api.Features.Auth.Requests;

public sealed record LoginRequest(
    [property: JsonRequired] string Email,
    [property: JsonRequired] string Password);
