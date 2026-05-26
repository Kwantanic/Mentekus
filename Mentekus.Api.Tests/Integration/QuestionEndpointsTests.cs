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
            .Setup(a => a.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding[0]);

        var request = new QuestionAskRequest(questionText);

        // Act
        var response = await Client.PostAsJsonAsync("/question/ask", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(questionText, content);
        Assert.Contains("Embedding length: 3", content);

        // Verify the mock was called
        OllamaAdapterMock.Verify(a => a.EmbedAsync(questionText, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Similarity_ReturnsMostSimilarQuestions()
    {
        // Arrange
        var question1 = "What is .NET?";
        var embedding1 = new float[] { 1.0f, 0.0f, 0.0f };
        var question2 = "What is Java?";
        var embedding2 = new float[] { 0.0f, 1.0f, 0.0f };
        var searchQuery = "Tell me about .NET";
        var searchEmbedding = new float[] { 0.9f, 0.1f, 0.0f };

        // 1. Setup mock for inserting questions
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(question1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding1);
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(question2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding2);

        await Client.PostAsJsonAsync("/question/ask", new QuestionAskRequest(question1));
        await Client.PostAsJsonAsync("/question/ask", new QuestionAskRequest(question2));

        // 2. Setup mock for similarity search
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(searchQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchEmbedding);

        var similarityRequest = new QuestionSimilarityRequest(searchQuery, 2);

        // Act
        var response = await Client.PostAsJsonAsync("/question/similarity", similarityRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<QuestionSimilarityResponse>>(
            Mentekus.Api.Serialization.AppJsonSerializerContext.Default.ListQuestionSimilarityResponse);

        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        Assert.Equal(question1, results[0].Text); // Should be more similar to .NET question
        Assert.True(results[0].Similarity > results[1].Similarity);
    }
}