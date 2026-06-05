using Injectio.Attributes;
using Mentekus.Api.Serialization;
using Microsoft.Extensions.Options;

namespace Mentekus.Api.Shared.Adapters;

[RegisterScoped(ServiceType = typeof(IOllamaAdapter))]
public class OllamaAdapter(HttpClient httpClient, IOptions<OllamaOptions> options) : IOllamaAdapter
{
    public async Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var model = options.Value.EmbeddingModel;

        var request = new OllamaEmbedRequest(model, text);

        using var response = await httpClient.PostAsJsonAsync(
            "api/embed",
            request,
            AppJsonSerializerContext.Default.OllamaEmbedRequest,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(
            AppJsonSerializerContext.Default.OllamaEmbedResponse,
            cancellationToken);

        return result?.Embeddings?.FirstOrDefault();
    }
}

public sealed record OllamaEmbedRequest(string Model, string Input);

public sealed record OllamaEmbedResponse(float[][]? Embeddings);