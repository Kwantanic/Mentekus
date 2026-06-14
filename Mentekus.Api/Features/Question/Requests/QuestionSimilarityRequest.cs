using System.Text.Json.Serialization;

namespace Mentekus.Api.Features.Question.Requests;

public sealed record QuestionSimilarityRequest(
    [property: JsonRequired] string Text,
    int Limit = 5);

public sealed record QuestionSimilarityResponse(
    string Text,
    double Similarity,
    Guid AskedByUserId,
    string AskedByEmail);