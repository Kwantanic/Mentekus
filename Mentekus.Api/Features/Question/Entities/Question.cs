using Pgvector;

namespace Mentekus.Api.Features.Question.Entities;

public record Question(
    Guid Id,
    string Text,
    Vector? Embedding,
    DateTime CreatedAt,
    Guid? AskedByUserId);