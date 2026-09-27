using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Mentekus.Api.Serialization;
using Microsoft.Extensions.Options;

namespace Mentekus.Api.Shared.Adapters;

// Registered by services.AddHttpClient, not Injectio. The typed client is what supplies HttpClient.
public class OllamaAdapter(HttpClient httpClient, IOptions<OllamaOptions> options) : IOllamaAdapter
{
    public async Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var request = new OllamaEmbedRequest(options.Value.EmbeddingModel, text, options.Value.KeepAlive);
        var result = await PostAsync(
            "api/embed",
            request,
            AppJsonSerializerContext.Default.OllamaEmbedRequest,
            AppJsonSerializerContext.Default.OllamaEmbedResponse,
            cancellationToken);

        return result?.Embeddings?.FirstOrDefault();
    }

    public async Task<string?> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var request = new OllamaGenerateRequest(options.Value.Model, prompt, options.Value.KeepAlive);
        var result = await PostAsync(
            "api/generate",
            request,
            AppJsonSerializerContext.Default.OllamaGenerateRequest,
            AppJsonSerializerContext.Default.OllamaGenerateResponse,
            cancellationToken);

        return result?.Response;
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string path,
        TRequest request,
        JsonTypeInfo<TRequest> requestType,
        JsonTypeInfo<TResponse> responseType,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(path, request, requestType, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new EmbeddingFailedException(
                    "The Ollama request failed.",
                    new HttpRequestException($"Ollama returned {(int)response.StatusCode}."));

            return await response.Content.ReadFromJsonAsync(responseType, cancellationToken);
        }
        catch (Exception exception) when (IsOllamaTimeout(exception, cancellationToken))
        {
            // HttpClient.Timeout cancels with its own token, which is not the caller's.
            throw new EmbeddingFailedException("The Ollama request timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new EmbeddingFailedException("The Ollama request failed.", exception);
        }
        catch (JsonException exception)
        {
            throw new EmbeddingFailedException("The Ollama request failed.", exception);
        }
    }

    private static bool IsOllamaTimeout(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested
        && (exception is TimeoutException || exception is OperationCanceledException);
}

public sealed record OllamaEmbedRequest(
    string Model,
    string Input,
    [property: JsonPropertyName("keep_alive")] string KeepAlive);

public sealed record OllamaEmbedResponse(float[][]? Embeddings);

public sealed record OllamaGenerateRequest(
    string Model,
    string Prompt,
    [property: JsonPropertyName("keep_alive")] string KeepAlive,
    bool Stream = false);

public sealed record OllamaGenerateResponse(string? Response);
