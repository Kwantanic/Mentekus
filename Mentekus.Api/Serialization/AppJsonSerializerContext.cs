using System.Text.Json.Serialization;
using Mentekus.Api.Features.Question.Requests;

namespace Mentekus.Api.Serialization;

[JsonSerializable(typeof(QuestionAskRequest))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}