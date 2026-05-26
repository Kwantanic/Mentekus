namespace Mentekus.Api.Shared.Adapters;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string? BaseUrl { get; set; }
    public string EmbeddingModel { get; set; } = "qwen3-embedding:0.6b";
}