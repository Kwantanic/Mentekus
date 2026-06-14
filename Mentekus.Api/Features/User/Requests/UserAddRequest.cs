using System.Text.Json.Serialization;
namespace Mentekus.Api.Features.User.Requests;

public sealed record UserAddRequest(
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Email);

public sealed record UserAddResponse(Guid Id, string Name, string Email);
