using System.Text.Json.Serialization;
using Mentekus.Api.Features.Question.Entities;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Shared.Adapters;
using Pgvector;

namespace Mentekus.Api.Serialization;

[JsonSerializable(typeof(QuestionAskRequest))]
[JsonSerializable(typeof(QuestionSimilarityRequest))]
[JsonSerializable(typeof(QuestionSimilarityResponse))]
[JsonSerializable(typeof(List<QuestionSimilarityResponse>))]
[JsonSerializable(typeof(OllamaEmbedRequest))]
[JsonSerializable(typeof(OllamaEmbedResponse))]
[JsonSerializable(typeof(Question))]
[JsonSerializable(typeof(List<Question>))]
[JsonSerializable(typeof(Vector))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}