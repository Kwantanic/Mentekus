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
        await Client.RegisterAndSignInAsync("Json User", "json@example.com");
        var malformedJson = "{ \"question\": \"What is AOT?\" ";
        var content = new StringContent(malformedJson, Encoding.UTF8, "application/json");

        var response = await Client.PostRawAsync("/question/ask", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Contains("The request contains malformed JSON:", problem.Detail);
    }

    [Fact]
    public async Task MissingNonNullableProperty_ReturnsFriendlyError()
    {
        await Client.RegisterAndSignInAsync("Json User", "json-missing@example.com");
        var missingPropertyJson = "{ }";
        var content = new StringContent(missingPropertyJson, Encoding.UTF8, "application/json");

        var response = await Client.PostRawAsync("/question/ask", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal("request.question is required", problem.Detail);
    }

    [Fact]
    public async Task MalformedJson_ValueMissing_ReturnsFriendlyError()
    {
        await Client.RegisterAndSignInAsync("Json User", "json-value@example.com");
        var malformedJson = "{ \"question\": }";
        var content = new StringContent(malformedJson, Encoding.UTF8, "application/json");

        var response = await Client.PostRawAsync("/question/ask", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Equal("request.question is malformed or missing", problem.Detail);
    }
}
