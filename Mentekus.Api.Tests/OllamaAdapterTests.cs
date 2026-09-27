using System.Net;
using System.Net.Http.Json;
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
        // Arrange
        var expectedResponse = new OllamaEmbedResponse(new[] { new[] { 0.1f, 0.2f } });
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(expectedResponse, AppJsonSerializerContext.Default.OllamaEmbedResponse)
        }));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        var options = Options.Create(new OllamaOptions { EmbeddingModel = "qwen3-embedding:0.6b" });
        var adapter = new OllamaAdapter(httpClient, options);
        // Act
        var result = await adapter.EmbedAsync("input");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal(0.1f, result[0]);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request);
    }
}