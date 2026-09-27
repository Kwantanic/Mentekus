namespace Mentekus.Api.Shared.Adapters;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";
    public const string DefaultKeepAlive = "30m";

    public string? BaseUrl { get; set; }
    public string Model { get; set; } = "qwen3:4b-instruct";
    public string EmbeddingModel { get; set; } = "qwen3-embedding:0.6b";
    public string KeepAlive { get; set; } = DefaultKeepAlive;
    public int TimeoutSeconds { get; set; } = 180;
}
