using Mentekus.Api.Features.Question.Entities;

namespace Mentekus.Api.Features.Question;

public sealed record QuestionCreated(Guid Id, DateTime CreatedAt);

public sealed record AnswerCreated(Guid Id, Guid QuestionId, DateTime CreatedAt);

public sealed record QuestionDetail(
    Guid Id,
    string Text,
    DateTime CreatedAt,
    Guid? AskedByUserId,
    string? AskedByEmail,
    List<QuestionAnswer> Answers);
