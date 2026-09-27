using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Mentekus.Api.Serialization;
using Mentekus.Api.Shared.Adapters;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mentekus.Api.Tests;

public class OllamaAdapterTests
{
    [Fact]
    public async Task EmbedAsync_ReturnsResponse_WhenApiReturnsOk()
    {
        string? body = null;
        var expectedResponse = new OllamaEmbedResponse([ [0.1f, 0.2f] ]);
        var handler = new StubHandler(async (request, _) =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(expectedResponse, AppJsonSerializerContext.Default.OllamaEmbedResponse)
            };
        });

        var adapter = CreateAdapter(handler);
        var result = await adapter.EmbedAsync("input");

        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal(0.1f, result[0]);
        Assert.NotNull(body);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("qwen3-embedding:0.6b", document.RootElement.GetProperty("model").GetString());
        Assert.Equal("input", document.RootElement.GetProperty("input").GetString());
        Assert.Equal(OllamaOptions.DefaultKeepAlive, document.RootElement.GetProperty("keep_alive").GetString());
    }

    [Fact]
    public async Task GenerateAsync_SendsKeepAlive_AndDoesNotStream()
    {
        string? body = null;
        var handler = new StubHandler(async (request, _) =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(
                    new OllamaGenerateResponse("topics"),
                    AppJsonSerializerContext.Default.OllamaGenerateResponse)
            };
        });

        var adapter = CreateAdapter(handler);
        var result = await adapter.GenerateAsync("extract topics");

        Assert.Equal("topics", result);
        Assert.NotNull(body);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("qwen3:4b-instruct", document.RootElement.GetProperty("model").GetString());
        Assert.Equal(OllamaOptions.DefaultKeepAlive, document.RootElement.GetProperty("keep_alive").GetString());
        Assert.False(document.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public async Task EmbedAsync_WhenOllamaReturnsError_ThrowsEmbeddingFailed()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError
        }));

        var adapter = CreateAdapter(handler);
        var exception = await Assert.ThrowsAsync<EmbeddingFailedException>(() => adapter.EmbedAsync("input"));

        Assert.Equal("The Ollama request failed.", exception.Message);
    }

    [Fact]
    public async Task EmbedAsync_WhenClientTimesOut_ThrowsEmbeddingFailed()
    {
        var handler = new StubHandler((_, _) => throw new TaskCanceledException());
        var adapter = CreateAdapter(handler);

        var exception = await Assert.ThrowsAsync<EmbeddingFailedException>(() => adapter.EmbedAsync("input"));

        Assert.Equal("The Ollama request timed out.", exception.Message);
    }

    [Fact]
    public async Task EmbedAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var handler = new StubHandler((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var adapter = CreateAdapter(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.EmbedAsync("input", cancellation.Token));
    }

    private static OllamaAdapter CreateAdapter(StubHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        var options = Options.Create(new OllamaOptions
        {
            EmbeddingModel = "qwen3-embedding:0.6b",
            Model = "qwen3:4b-instruct"
        });
        return new OllamaAdapter(httpClient, options);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
