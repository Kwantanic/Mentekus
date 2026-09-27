using Mentekus.Api.Shared.Adapters;

namespace Mentekus.Api.Tests.Integration;

public sealed class FakeOllamaAdapter : IOllamaAdapter
{
    private readonly List<string> _embedCalls = [];
    private readonly List<string> _generateCalls = [];
    private readonly List<EmbedRule> _embedRules = [];
    private readonly List<GenerateRule> _generateRules = [];

    public IReadOnlyList<string> EmbedCalls => _embedCalls;
    public IReadOnlyList<string> GenerateCalls => _generateCalls;

    public void Embed(string text, float[] embedding) => Embed(candidate => candidate == text, _ => embedding);

    public void Embed(Func<string, bool> when, Func<string, float[]?> embedding) =>
        _embedRules.Add(new EmbedRule(when, (text, _) => Task.FromResult(embedding(text))));

    public void EmbedAny(float[] embedding) => Embed(_ => true, _ => embedding);

    public void EmbedThrows(Func<string, bool> when, Exception exception) =>
        _embedRules.Add(new EmbedRule(when, (_, _) => Task.FromException<float[]?>(exception)));

    public void GenerateAny(string response) => Generate(_ => true, _ => response);

    public void Generate(Func<string, bool> when, Func<string, string?> response) =>
        _generateRules.Add(new GenerateRule(when, (prompt, _) => Task.FromResult(response(prompt))));

    public void GenerateThrows(Func<string, bool> when, Exception exception) =>
        _generateRules.Add(new GenerateRule(when, (_, _) => Task.FromException<string?>(exception)));

    public Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        _embedCalls.Add(text);
        for (var i = _embedRules.Count - 1; i >= 0; i--)
        {
            if (_embedRules[i].When(text))
                return _embedRules[i].Handle(text, cancellationToken);
        }

        return Task.FromResult<float[]?>(null);
    }

    public Task<string?> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        _generateCalls.Add(prompt);
        for (var i = _generateRules.Count - 1; i >= 0; i--)
        {
            if (_generateRules[i].When(prompt))
                return _generateRules[i].Handle(prompt, cancellationToken);
        }

        return Task.FromResult<string?>(null);
    }

    private sealed record EmbedRule(Func<string, bool> When, Func<string, CancellationToken, Task<float[]?>> Handle);

    private sealed record GenerateRule(Func<string, bool> When, Func<string, CancellationToken, Task<string?>> Handle);
}
