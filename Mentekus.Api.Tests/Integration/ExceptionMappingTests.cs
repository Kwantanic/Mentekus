using System.Net;
using System.Net.Http.Json;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Mentekus.Api.Serialization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Mentekus.Api.Tests.Integration;

public class ExceptionMappingTests : IntegrationTestBase
{
    private const string TestEmail = "exception.test@example.com";

    private async Task EnsureUserExists()
    {
        await Client.RegisterAndSignInAsync("Exception Test", TestEmail);
    }

    [Fact]
    public async Task NotFoundException_Returns404()
    {
        // Arrange
        await EnsureUserExists();
        Ollama.EmbedThrows(s => s == "trigger-notfound", new NotFoundException("Test Entity", 123));

        // Act
        var response = await Client.PostJsonAsync("/question/ask",
            new QuestionAskRequest("trigger-notfound"), AppJsonSerializerContext.Default.QuestionAskRequest);

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
        Ollama.EmbedThrows(s => s == "trigger-unauthorized", new UnauthorizedAccessException("Test Unauthorized"));

        // Act
        var response = await Client.PostJsonAsync("/question/ask",
            new QuestionAskRequest("trigger-unauthorized"),
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
        await EnsureUserExists();
        Ollama.EmbedThrows(s => s == "trigger-argument", new ArgumentException("Test Argument"));

        var response = await Client.PostJsonAsync("/question/ask",
            new QuestionAskRequest("trigger-argument"), AppJsonSerializerContext.Default.QuestionAskRequest);

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
        Ollama.EmbedThrows(s => s == "trigger-validation", new ValidationException(errors));

        // Act
        var response = await Client.PostJsonAsync("/question/ask",
            new QuestionAskRequest("trigger-validation"),
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