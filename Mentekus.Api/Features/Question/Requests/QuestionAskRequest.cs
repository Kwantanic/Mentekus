using System.Text.Json.Serialization;
namespace Mentekus.Api.Features.Question.Requests;

public sealed record QuestionAskRequest(
    [property: JsonRequired] string Question,
    [property: JsonRequired] string Email);