using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Mentekus.Api.Serialization;
using System.Net.Http.Json;

namespace Mentekus.Api.Tests.Integration;

public class ErrorHandlingTests : IntegrationTestBase
{
    [Fact]
    public async Task MalformedJson_ReturnsFriendlyError()
    {
        // Arrange
        var malformedJson = "{ \"Question\": \"What is AOT?\", \"Email\": \"test@example.com\" "; // Missing closing brace
        var content = new StringContent(malformedJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/question/ask", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Contains("The request contains malformed JSON:", problem.Detail);
    }

    [Fact]
    public async Task MissingNonNullableProperty_ReturnsFriendlyError()
    {
        // Arrange
        var missingPropertyJson = "{ \"Email\": \"test@example.com\" }"; // Question is missing
        var content = new StringContent(missingPropertyJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/question/ask", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal("request.question is required", problem.Detail);
    }

    [Fact]
    public async Task MalformedJson_ValueMissing_ReturnsFriendlyError()
    {
        // Arrange
        var malformedJson = "{ \"Question\": \"What is AOT?\", \"Email\": }"; // Missing value
        var content = new StringContent(malformedJson, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/question/ask", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Equal("request.Email is malformed or missing", problem.Detail);
    }
}
