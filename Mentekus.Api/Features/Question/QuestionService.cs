using System.Text.Json.Serialization;
using Mentekus.Api.Serialization;

namespace Mentekus.Api.Features.Question;

public interface IQuestionService
{
    Task<string> AskAsync(string question, CancellationToken cancellationToken = default);
}

public class QuestionService(HttpClient httpClient, IConfiguration configuration) : IQuestionService
{
    public async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        var model = configuration["Ollama:EmbeddingModel"] ?? "qwen3-embedding:0.6b";

        var request = new OllamaEmbedRequest(
            model,
            question);

        using var response = await httpClient.PostAsJsonAsync(
            "api/embed",
            request,
            AppJsonSerializerContext.Default.OllamaEmbedRequest,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(
            AppJsonSerializerContext.Default.OllamaEmbedResponse,
            cancellationToken);

        var embeddingLength = result?.Embeddings?.FirstOrDefault()?.Length ?? 0;

        return $"You asked: {question}. Embedding length: {embeddingLength}";
    }
}

// TODO: move these
internal sealed record OllamaEmbedRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("input")] string Input);

internal sealed record OllamaEmbedResponse(
    [property: JsonPropertyName("embeddings")]
    float[][]? Embeddings);