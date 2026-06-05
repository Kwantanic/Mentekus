namespace Mentekus.Api.Features.Question.Requests;

public sealed record QuestionAskRequest(
    string Question,
    string? Name = null,
    string? Email = null);