using System.Net;
using System.Net.Http.Json;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Features.User.Requests;
using Mentekus.Api.Serialization;
using Mentekus.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Mentekus.Api.Tests.Integration;

public class ExceptionMappingTests : IntegrationTestBase
{
    private const string TestEmail = "exception.test@example.com";

    private async Task EnsureUserExists()
    {
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("Exception Test", TestEmail),
            AppJsonSerializerContext.Default.UserAddRequest);
    }

    [Fact]
    public async Task NotFoundException_Returns404()
    {
        // Arrange
        await EnsureUserExists();
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(It.Is<string>(s => s == "trigger-notfound"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Test Entity", 123));

        // Act
        var response = await Client.PostAsJsonAsync("/question/ask",
            new QuestionAskRequest("trigger-notfound", TestEmail), AppJsonSerializerContext.Default.QuestionAskRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem =
            await response.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Equal("Not Found", problem.Title);
        Assert.Contains("Test Entity", problem.Detail);
    }

    [Fact]
    public async Task UnauthorizedAccessException_Returns401()
    {
        // Arrange
        await EnsureUserExists();
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(It.Is<string>(s => s == "trigger-unauthorized"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Test Unauthorized"));

        // Act
        var response = await Client.PostAsJsonAsync("/question/ask",
            new QuestionAskRequest("trigger-unauthorized", TestEmail),
            AppJsonSerializerContext.Default.QuestionAskRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem =
            await response.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Equal("Unauthorized", problem.Title);
    }

    [Fact]
    public async Task ArgumentException_Returns400()
    {
        // Arrange
        // QuestionService throws ArgumentException if email is null/empty, 
        // but QuestionEndpoints also validates it. 
        // Let's mock OllamaAdapter to throw it.
        await EnsureUserExists();
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(It.Is<string>(s => s == "trigger-argument"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Test Argument"));

        // Act
        var response = await Client.PostAsJsonAsync("/question/ask",
            new QuestionAskRequest("trigger-argument", TestEmail), AppJsonSerializerContext.Default.QuestionAskRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem =
            await response.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Contains("Test Argument", problem.Detail);
    }

    [Fact]
    public async Task ValidationException_MultipleErrors_Returns400WithErrors()
    {
        // Arrange
        await EnsureUserExists();
        var errors = new Dictionary<string, string[]>
        {
            { "Prop1", ["Error 1", "Error 2"] },
            { "Prop2", ["Error 3"] }
        };
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(It.Is<string>(s => s == "trigger-validation"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(errors));

        // Act
        var response = await Client.PostAsJsonAsync("/question/ask",
            new QuestionAskRequest("trigger-validation", TestEmail),
            AppJsonSerializerContext.Default.QuestionAskRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem =
            await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(AppJsonSerializerContext.Default
                .ValidationProblemDetails);
        Assert.NotNull(problem);
        Assert.Equal("Validation Error", problem.Title);
        Assert.Equal(2, problem.Errors.Count);
        Assert.Equal(2, problem.Errors["Prop1"].Length);
        Assert.Single(problem.Errors["Prop2"]);
    }
}