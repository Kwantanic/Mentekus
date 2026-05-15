using System.Text.Json.Serialization;
using Mentekus.Api.Features.Question;
using Mentekus.Api.Features.Question.Requests;

namespace Mentekus.Api.Serialization;

[JsonSerializable(typeof(QuestionAskRequest))]
[JsonSerializable(typeof(OllamaEmbedRequest))]
[JsonSerializable(typeof(OllamaEmbedResponse))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}