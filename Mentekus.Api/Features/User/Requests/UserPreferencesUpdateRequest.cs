namespace Mentekus.Api.Features.User.Requests;

public sealed record UserPreferencesUpdateRequest(
    bool? ProfileVisible = null,
    bool? AllowRouting = null);
