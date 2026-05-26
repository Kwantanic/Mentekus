using Mentekus.Api.Serialization;
using Microsoft.Extensions.Configuration;

namespace Mentekus.Api.Shared.Adapters;

public interface IOllamaAdapter
{
    Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken = default);
}

public class OllamaAdapter(HttpClient httpClient, IConfiguration configuration) : IOllamaAdapter
{
    public async Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var model = configuration["Ollama:EmbeddingModel"] ?? "qwen3-embedding:0.6b";

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