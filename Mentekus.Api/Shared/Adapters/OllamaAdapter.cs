using Mentekus.Api.Serialization;
using Microsoft.Extensions.Options;

namespace Mentekus.Api.Shared.Adapters;

// Registered by services.AddHttpClient, not Injectio. The typed client is what supplies HttpClient.
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

    public async Task<string?> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var model = options.Value.Model;

        var request = new OllamaGenerateRequest(model, prompt);

        using var response = await httpClient.PostAsJsonAsync(
            "api/generate",
            request,
            AppJsonSerializerContext.Default.OllamaGenerateRequest,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(
            AppJsonSerializerContext.Default.OllamaGenerateResponse,
            cancellationToken);

        return result?.Response;
    }
}

public sealed record OllamaEmbedRequest(string Model, string Input);

public sealed record OllamaEmbedResponse(float[][]? Embeddings);

public sealed record OllamaGenerateRequest(string Model, string Prompt, bool Stream = false);

public sealed record OllamaGenerateResponse(string? Response);