using System.Net;
using System.Net.Http.Json;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Shared.Adapters;
using Moq;
using Xunit;

namespace Mentekus.Api.Tests.Integration;

public class QuestionEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task Ask_ReturnsOk_AndSavesToDatabase()
    {
        // Arrange
        var questionText = "What is Native AOT?";
        var expectedEmbedding = new[] { new[] { 0.1f, 0.2f, 0.3f } };

        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(It.IsAny<OllamaEmbedRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaEmbedResponse(expectedEmbedding));

        var request = new QuestionAskRequest(questionText);

        // Act
        var response = await Client.PostAsJsonAsync("/question/ask", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(questionText, content);
        Assert.Contains("Embedding length: 3", content);

        // Verify the mock was called
        OllamaAdapterMock.Verify(a => a.EmbedAsync(
                It.Is<OllamaEmbedRequest>(r => r.Input == questionText),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}