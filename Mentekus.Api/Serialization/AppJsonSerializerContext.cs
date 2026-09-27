using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using Mentekus.Api.Features.Auth;
using Mentekus.Api.Features.Auth.Requests;
using Mentekus.Api.Features.Expertise.Entities;
using Mentekus.Api.Features.Expertise.Requests;
using Mentekus.Api.Features.Question;
using Mentekus.Api.Features.Question.Entities;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Features.User;
using Mentekus.Api.Shared.Adapters;
using Pgvector;

namespace Mentekus.Api.Serialization;

[JsonSerializable(typeof(QuestionAskRequest))]
[JsonSerializable(typeof(QuestionSimilarityRequest))]
[JsonSerializable(typeof(QuestionSimilarity))]
[JsonSerializable(typeof(List<QuestionSimilarity>))]
[JsonSerializable(typeof(QuestionAnswerRequest))]
[JsonSerializable(typeof(QuestionCreated))]
[JsonSerializable(typeof(AnswerCreated))]
[JsonSerializable(typeof(QuestionDetail))]
[JsonSerializable(typeof(QuestionAnswer))]
[JsonSerializable(typeof(List<QuestionAnswer>))]
[JsonSerializable(typeof(OllamaEmbedRequest))]
[JsonSerializable(typeof(OllamaEmbedResponse))]
[JsonSerializable(typeof(OllamaGenerateRequest))]
[JsonSerializable(typeof(OllamaGenerateResponse))]
[JsonSerializable(typeof(Vector))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(AuthUser))]
[JsonSerializable(typeof(AuthSessionResponse))]
[JsonSerializable(typeof(XsrfTokenResponse))]
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
