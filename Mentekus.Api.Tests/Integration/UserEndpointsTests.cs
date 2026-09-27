using System.Net;
using System.Net.Http.Json;
using Mentekus.Api.Features.Expertise;
using Mentekus.Api.Features.Expertise.Entities;
using Mentekus.Api.Features.User;
using Mentekus.Api.Features.User.Requests;
using Mentekus.Api.Serialization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Mentekus.Api.Tests.Integration;

public class UserEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task AddUser_ReturnsOk_AndSavesToDatabase()
    {
        // Arrange
        var request = new UserAddRequest("John Doe", "john@example.com");

        // Act
        var response = await Client.PostAsJsonAsync("/user/add", request, AppJsonSerializerContext.Default.UserAddRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.UserAddResponse);
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.Email, result.Email);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task AddUser_DuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new UserAddRequest("John Doe", "john@example.com");
        await Client.PostAsJsonAsync("/user/add", request, AppJsonSerializerContext.Default.UserAddRequest);

        // Act - same email, different case
        var duplicateRequest = new UserAddRequest("Jane Doe", "JOHN@example.com");
        var response = await Client.PostAsJsonAsync("/user/add", duplicateRequest, AppJsonSerializerContext.Default.UserAddRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(AppJsonSerializerContext.Default.ValidationProblemDetails);
        Assert.NotNull(problem);
        Assert.Contains("already exists", problem.Detail);
        Assert.True(problem.Errors.ContainsKey("Email"));
    }

    [Fact]
    public async Task GetUserExpertiseProfile_ReturnsOk_WithTopicsAfterContribution()
    {
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("Prof User", "prof@example.com"), AppJsonSerializerContext.Default.UserAddRequest);

        Ollama.EmbedAny(Enumerable.Repeat(0.2f, 1024).ToArray());
        Ollama.GenerateAny("[\"topicx\", \"topicy\"]");

        await Client.PostAsJsonAsync("/expertise/document", new ExpertiseDocumentIngestRequest("some prof doc", "prof@example.com"), AppJsonSerializerContext.Default.ExpertiseDocumentIngestRequest);

        var resp = await Client.GetAsync("/user/prof@example.com/expertise");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var prof = await resp.Content.ReadFromJsonAsync<UserExpertiseProfile>(AppJsonSerializerContext.Default.UserExpertiseProfile);
        Assert.NotNull(prof);
        Assert.Equal("Prof User", prof.Name);
        Assert.Contains("topicx", prof.TopTopics);
    }

    [Fact]
    public async Task UpdateUserPreferences_AllowsTogglingRoutingFlag()
    {
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("Pref User", "pref@example.com"), AppJsonSerializerContext.Default.UserAddRequest);

        var updateReq = new UserPreferencesUpdateRequest(ProfileVisible: true, AllowRouting: false);
        var updResp = await Client.PostAsJsonAsync("/user/pref@example.com/preferences", updateReq, AppJsonSerializerContext.Default.UserPreferencesUpdateRequest);
        Assert.Equal(HttpStatusCode.OK, updResp.StatusCode);
        var msg = await updResp.Content.ReadAsStringAsync();
        Assert.Contains("Preferences updated", msg);

        // verify by routing (should exclude)
        Ollama.EmbedAny(Enumerable.Repeat(0.3f, 1024).ToArray());
        await Client.PostAsJsonAsync("/expertise/document", new ExpertiseDocumentIngestRequest("pref doc for route test", "pref@example.com"), AppJsonSerializerContext.Default.ExpertiseDocumentIngestRequest);

        var routeResp = await Client.PostAsJsonAsync("/expertise/route", new ExpertiseRouteRequest("pref doc for route test", 3), AppJsonSerializerContext.Default.ExpertiseRouteRequest);
        var matches = await routeResp.Content.ReadFromJsonAsync<List<ExpertiseRouteMatch>>(AppJsonSerializerContext.Default.ListExpertiseRouteMatch);
        Assert.NotNull(matches);
        Assert.DoesNotContain(matches, m => m.Email == "pref@example.com");
    }
}
