namespace Mentekus.Api.Features.Question.Entities;

public record QuestionAnswer(
    Guid Id,
    string Text,
    DateTime CreatedAt,
    Guid? AnsweredByUserId,
    string? AnsweredByEmail);
