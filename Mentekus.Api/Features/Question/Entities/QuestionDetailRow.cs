namespace Mentekus.Api.Features.Question.Entities;

internal record QuestionDetailRow(
    Guid Id,
    string Text,
    DateTime CreatedAt,
    Guid? AskedByUserId,
    string? AskedByEmail);
