using Mentekus.Api.Shared.Adapters;

namespace Mentekus.Api.Features.Question;

public interface IQuestionService
{
    Task<string> AskAsync(string question, CancellationToken cancellationToken = default);
}

public class QuestionService(IOllamaAdapter ollamaAdapter, IConfiguration configuration) : IQuestionService
{
    public async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        var model = configuration["Ollama:EmbeddingModel"] ?? "qwen3-embedding:0.6b";

        var request = new OllamaEmbedRequest(
            model,
            question);

        var result = await ollamaAdapter.EmbedAsync(request, cancellationToken);

        var embeddingLength = result?.Embeddings?.FirstOrDefault()?.Length ?? 0;

        return $"You asked: {question}. Embedding length: {embeddingLength}";
    }
}