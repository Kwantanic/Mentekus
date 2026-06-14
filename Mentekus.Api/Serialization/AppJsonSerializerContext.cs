using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using Mentekus.Api.Features.Expertise;
using Mentekus.Api.Features.Expertise.Entities;
using Mentekus.Api.Features.Question.Entities;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Features.User;
using Mentekus.Api.Features.User.Requests;
using Mentekus.Api.Shared.Adapters;
using Pgvector;

namespace Mentekus.Api.Serialization;

[JsonSerializable(typeof(QuestionAskRequest))]
[JsonSerializable(typeof(QuestionSimilarityRequest))]
[JsonSerializable(typeof(QuestionSimilarity))]
[JsonSerializable(typeof(List<QuestionSimilarity>))]
[JsonSerializable(typeof(QuestionAnswerRequest))]
[JsonSerializable(typeof(OllamaEmbedRequest))]
[JsonSerializable(typeof(OllamaEmbedResponse))]
[JsonSerializable(typeof(OllamaGenerateRequest))]
[JsonSerializable(typeof(OllamaGenerateResponse))]
[JsonSerializable(typeof(Vector))]
[JsonSerializable(typeof(UserAddRequest))]
[JsonSerializable(typeof(UserAddResponse))]
[JsonSerializable(typeof(UserPreferencesUpdateRequest))]
[JsonSerializable(typeof(UserExpertiseProfile))]
[JsonSerializable(typeof(List<UserExpertiseProfile>))]
[JsonSerializable(typeof(ExpertiseDocumentIngestRequest))]
[JsonSerializable(typeof(ExpertiseRouteRequest))]
[JsonSerializable(typeof(ExpertiseRouteMatch))]
[JsonSerializable(typeof(List<ExpertiseRouteMatch>))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(ValidationProblemDetails))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}