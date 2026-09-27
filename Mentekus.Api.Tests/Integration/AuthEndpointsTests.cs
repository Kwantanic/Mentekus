using System.Net;
using System.Net.Http.Json;
using Mentekus.Api.Features.Auth;
using Mentekus.Api.Features.Auth.Requests;
using Mentekus.Api.Serialization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Mentekus.Api.Tests.Integration;

public class AuthEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task Register_SetsHttpOnlyCookies_AndSignsIn()
    {
        var xsrf = await Client.Http.GetAsync("/auth/xsrf");
        Assert.Equal(HttpStatusCode.OK, xsrf.StatusCode);
        Assert.Contains(xsrf.Headers.GetValues("Set-Cookie"), cookie =>
            cookie.Contains(AuthCookies.Xsrf, StringComparison.OrdinalIgnoreCase)
            && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        var shortPassword = await Client.PostJsonAsync(
            "/auth/register",
            new RegisterRequest("Ada Lovelace", "ada@example.com", "short"),
            AppJsonSerializerContext.Default.RegisterRequest);
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);

        var response = await Client.RegisterAndSignInAsync("Ada Lovelace", "ada@example.com");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie =>
            cookie.Contains(AuthCookies.Session, StringComparison.OrdinalIgnoreCase)
            && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        var me = await Client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var user = await me.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.AuthUser);
        Assert.NotNull(user);
        Assert.Equal("Ada Lovelace", user.Name);
        Assert.Equal("ada@example.com", user.Email);
        Assert.NotEqual(Guid.Empty, user.Id);

        var logout = await Client.LogoutAsync();
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        var signedOut = await Client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);

        var wrongPassword = await Client.PostJsonAsync(
            "/auth/login",
            new LoginRequest("ada@example.com", "wrong-password"),
            AppJsonSerializerContext.Default.LoginRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        var login = await Client.PostJsonAsync(
            "/auth/login",
            new LoginRequest("ADA@example.com", SessionClient.DefaultPassword),
            AppJsonSerializerContext.Default.LoginRequest);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.AuthSessionResponse);
        Assert.NotNull(session);
        Assert.Equal("ada@example.com", session.Email);
        Assert.False(string.IsNullOrEmpty(session.XsrfToken));

        var meAgain = await Client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, meAgain.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        await Client.RegisterAndSignInAsync("John Doe", "john@example.com");

        var response = await Client.PostJsonAsync(
            "/auth/register",
            new RegisterRequest("Jane Doe", "JOHN@example.com", SessionClient.DefaultPassword),
            AppJsonSerializerContext.Default.RegisterRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.ValidationProblemDetails);
        Assert.NotNull(problem);
        Assert.Contains("already exists", problem.Detail);
        Assert.True(problem.Errors.ContainsKey("Email"));
    }

    [Fact]
    public async Task MutatingRequest_WithoutXsrf_ReturnsBadRequest()
    {
        var anonymous = await Client.PostJsonWithoutXsrfAsync(
            "/auth/register",
            new RegisterRequest("No Token", "notoken@example.com", SessionClient.DefaultPassword),
            AppJsonSerializerContext.Default.RegisterRequest);
        Assert.Equal(HttpStatusCode.BadRequest, anonymous.StatusCode);
        var anonymousProblem = await anonymous.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(anonymousProblem);
        Assert.Contains("XSRF", anonymousProblem.Detail);

        await Client.RegisterAndSignInAsync("Token User", "token@example.com");
        var authenticated = await Client.PostJsonWithoutXsrfAsync(
            "/auth/logout",
            new LoginRequest("token@example.com", SessionClient.DefaultPassword),
            AppJsonSerializerContext.Default.LoginRequest);
        Assert.Equal(HttpStatusCode.BadRequest, authenticated.StatusCode);

        var me = await Client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }
}
