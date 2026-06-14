using System.Net;
using System.Net.Http.Json;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Features.User.Requests;
using Mentekus.Api.Serialization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Mentekus.Api.Tests.Integration;

public class QuestionEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task Ask_ReturnsOk_AndSavesToDatabase()
    {
        // 1. Add user
        var userRequest = new UserAddRequest("Test User", "test@example.com");
        await Client.PostAsJsonAsync("/user/add", userRequest);

        // 2. Ask question
        var questionText = "What is Native AOT?";
        var expectedEmbedding = new[] { new[] { 0.1f, 0.2f, 0.3f } };

        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding[0]);

        var request = new QuestionAskRequest(questionText, "test@example.com");

        // Act
        var response = await Client.PostAsJsonAsync("/question/ask", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Question saved (ID:", content);
        Assert.Contains("Embedding length: 3", content);

        // Verify the mock was called
        OllamaAdapterMock.Verify(a => a.EmbedAsync(questionText, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Similarity_ReturnsMostSimilarQuestions()
    {
        // Arrange
        var question1 = "What is .NET?";
        var embedding1 = new[] { 1.0f, 0.0f, 0.0f };
        var question2 = "What is Java?";
        var embedding2 = new[] { 0.0f, 1.0f, 0.0f };
        var searchQuery = "Tell me about .NET";
        var searchEmbedding = new[] { 0.9f, 0.1f, 0.0f };

        // 0. Add users
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("User One", "one@example.com"));
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("User Two", "two@example.com"));

        // 1. Setup mock for inserting questions
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(question1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding1);
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(question2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding2);

        await Client.PostAsJsonAsync("/question/ask", new QuestionAskRequest(question1, "one@example.com"));
        await Client.PostAsJsonAsync("/question/ask", new QuestionAskRequest(question2, "two@example.com"));

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
            AppJsonSerializerContext.Default.ListQuestionSimilarityResponse);

        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        Assert.Equal(question1, results[0].Text); // Should be more similar to .NET question
        Assert.Equal("one@example.com", results[0].AskedByEmail);
        Assert.NotEqual(Guid.Empty, results[0].AskedByUserId);
        Assert.Equal("two@example.com", results[1].AskedByEmail);
        Assert.NotEqual(Guid.Empty, results[1].AskedByUserId);
        Assert.True(results[0].Similarity > results[1].Similarity);
    }

    [Fact]
    public async Task Ask_ReturnsOk_CaseInsensitiveEmail()
    {
        // 1. Add user with mixed case
        var userRequest = new UserAddRequest("Mixed User", "Mixed@Example.Com");
        await Client.PostAsJsonAsync("/user/add", userRequest);

        // 2. Ask question with different case
        var questionText = "Case sensitivity test";
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[3]);

        var request = new QuestionAskRequest(questionText, "mixed@example.com");

        // Act
        var response = await Client.PostAsJsonAsync("/question/ask", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ask_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new QuestionAskRequest("Some question", "nonexistent@example.com");

        // Act
        var response = await Client.PostAsJsonAsync("/question/ask", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem =
            await response.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Contains("not found", problem.Detail);
    }
}