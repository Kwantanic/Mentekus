using System.Text.Json.Serialization;

namespace Mentekus.Api.Features.Expertise.Requests;

public sealed record ExpertiseDocumentIngestRequest(
    [property: JsonRequired] string Text);
