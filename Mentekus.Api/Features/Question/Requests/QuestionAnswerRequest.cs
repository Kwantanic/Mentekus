using System.Text.Json.Serialization;

namespace Mentekus.Api.Features.Question.Requests;

public sealed record QuestionAnswerRequest(
    [property: JsonRequired] Guid QuestionId,
    [property: JsonRequired] string Answer);
