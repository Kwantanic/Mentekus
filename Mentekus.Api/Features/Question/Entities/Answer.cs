using Pgvector;

namespace Mentekus.Api.Features.Question.Entities;

internal record Answer(
    Guid Id,
    Guid QuestionId,
    string Text,
    Vector Embedding,
    DateTime CreatedAt,
    Guid AnsweredByUserId);
