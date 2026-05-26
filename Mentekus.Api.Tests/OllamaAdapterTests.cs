using System.Net;
using System.Net.Http.Json;
using Mentekus.Api.Serialization;
using Mentekus.Api.Shared.Adapters;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Xunit;

namespace Mentekus.Api.Tests;

public class OllamaAdapterTests
{
    [Fact]
    public async Task EmbedAsync_ReturnsResponse_WhenApiReturnsOk()
    {
        // Arrange
        var expectedResponse = new OllamaEmbedResponse(new[] { new[] { 0.1f, 0.2f } });
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(expectedResponse, AppJsonSerializerContext.Default.OllamaEmbedResponse)
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        var adapter = new OllamaAdapter(httpClient, Mock.Of<IConfiguration>());
        // Act
        var result = await adapter.EmbedAsync("input");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal(0.1f, result[0]);
    }
}