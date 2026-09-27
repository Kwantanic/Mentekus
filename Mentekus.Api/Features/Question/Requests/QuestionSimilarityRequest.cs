using System.Text.Json.Serialization;

namespace Mentekus.Api.Features.Question.Requests;

public sealed record QuestionSimilarityRequest(
    [property: JsonRequired] string Text,
    int Limit = 5);
