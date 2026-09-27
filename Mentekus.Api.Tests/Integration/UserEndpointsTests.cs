using System.Net;
using System.Net.Http.Json;
using Mentekus.Api.Features.Expertise.Entities;
using Mentekus.Api.Features.Expertise.Requests;
using Mentekus.Api.Features.User;
using Mentekus.Api.Serialization;
using Xunit;

namespace Mentekus.Api.Tests.Integration;

public class UserEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task GetUserExpertiseProfile_ReturnsOk_WithTopicsAfterContribution()
    {
        await Client.RegisterAndSignInAsync("Prof User", "prof@example.com");

        Ollama.EmbedAny(Enumerable.Repeat(0.2f, 1024).ToArray());
        Ollama.GenerateAny("[\"topicx\", \"topicy\"]");

        await Client.PostJsonAsync("/expertise/document", new ExpertiseDocumentIngestRequest("some prof doc"), AppJsonSerializerContext.Default.ExpertiseDocumentIngestRequest);

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
        await Client.RegisterAndSignInAsync("Pref User", "pref@example.com");

        var updateReq = new UserPreferencesUpdateRequest(ProfileVisible: true, AllowRouting: false);
        var updResp = await Client.PostJsonAsync("/user/pref@example.com/preferences", updateReq, AppJsonSerializerContext.Default.UserPreferencesUpdateRequest);
        Assert.Equal(HttpStatusCode.OK, updResp.StatusCode);
        var msg = await updResp.Content.ReadAsStringAsync();
        Assert.Contains("Preferences updated", msg);

        Ollama.EmbedAny(Enumerable.Repeat(0.3f, 1024).ToArray());
        await Client.PostJsonAsync("/expertise/document", new ExpertiseDocumentIngestRequest("pref doc for route test"), AppJsonSerializerContext.Default.ExpertiseDocumentIngestRequest);

        var routeResp = await Client.PostJsonAsync("/expertise/route", new ExpertiseRouteRequest("pref doc for route test", 3), AppJsonSerializerContext.Default.ExpertiseRouteRequest);
        var matches = await routeResp.Content.ReadFromJsonAsync<List<ExpertiseRouteMatch>>(AppJsonSerializerContext.Default.ListExpertiseRouteMatch);
        Assert.NotNull(matches);
        Assert.DoesNotContain(matches, m => m.Email == "pref@example.com");
    }

    [Fact]
    public async Task UpdateUserPreferences_OtherUser_ReturnsForbidden()
    {
        await Client.RegisterAndSignInAsync("Owner", "owner@example.com");
        var other = CreateSession();
        await other.RegisterAndSignInAsync("Other", "other@example.com");

        var response = await other.PostJsonAsync(
            "/user/owner@example.com/preferences",
            new UserPreferencesUpdateRequest(AllowRouting: false),
            AppJsonSerializerContext.Default.UserPreferencesUpdateRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
