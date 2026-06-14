namespace Mentekus.Api.Features.Question.Entities;

public record QuestionSimilarity(
    string Text,
    double Similarity,
    Guid AskedByUserId,
    string AskedByEmail);
