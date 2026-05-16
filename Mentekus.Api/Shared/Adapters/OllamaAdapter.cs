using Mentekus.Api.Serialization;

namespace Mentekus.Api.Shared.Adapters;

public interface IOllamaAdapter
{
    Task<OllamaEmbedResponse?> EmbedAsync(OllamaEmbedRequest request, CancellationToken cancellationToken = default);
}

public class OllamaAdapter(HttpClient httpClient) : IOllamaAdapter
{
    public async Task<OllamaEmbedResponse?> EmbedAsync(OllamaEmbedRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/embed",
            request,
            AppJsonSerializerContext.Default.OllamaEmbedRequest,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            AppJsonSerializerContext.Default.OllamaEmbedResponse,
            cancellationToken);
    }
}

public sealed record OllamaEmbedRequest(string Model, string Input);

public sealed record OllamaEmbedResponse(float[][]? Embeddings);