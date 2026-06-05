namespace Mentekus.Api.Shared.Adapters;

public interface IOllamaAdapter
{
    Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken = default);
}