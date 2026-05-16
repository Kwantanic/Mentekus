using System.Text.Json.Serialization;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Shared.Adapters;

namespace Mentekus.Api.Serialization;

[JsonSerializable(typeof(QuestionAskRequest))]
[JsonSerializable(typeof(OllamaEmbedRequest))]
[JsonSerializable(typeof(OllamaEmbedResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}