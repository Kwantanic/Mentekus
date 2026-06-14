namespace Mentekus.Api.Features.User.Requests;

public sealed record UserAddRequest(string Name, string Email);

public sealed record UserAddResponse(Guid Id, string Name, string Email);
