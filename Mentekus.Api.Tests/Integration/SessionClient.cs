using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using Mentekus.Api.Features.Auth;
using Mentekus.Api.Features.Auth.Requests;
using Mentekus.Api.Serialization;

namespace Mentekus.Api.Tests.Integration;

public sealed class SessionClient
{
    public const string DefaultPassword = "test-password";

    private readonly HttpClient _http;
    private string? _xsrf;

    public SessionClient(HttpClient http) => _http = http;

    public HttpClient Http => _http;

    public async Task<HttpResponseMessage> GetAsync(string url) => await _http.GetAsync(url);

    public async Task<HttpResponseMessage> PostJsonAsync<T>(string url, T body, JsonTypeInfo<T> typeInfo)
    {
        await EnsureXsrfAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, typeInfo)
        };
        request.Headers.TryAddWithoutValidation(AuthCookies.XsrfHeader, _xsrf);
        return await _http.SendAsync(request);
    }

    public async Task<HttpResponseMessage> PostRawAsync(string url, HttpContent content)
    {
        await EnsureXsrfAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        request.Headers.TryAddWithoutValidation(AuthCookies.XsrfHeader, _xsrf);
        return await _http.SendAsync(request);
    }

    public async Task<HttpResponseMessage> PostJsonWithoutXsrfAsync<T>(string url, T body, JsonTypeInfo<T> typeInfo)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, typeInfo)
        };
        return await _http.SendAsync(request);
    }

    public Task<HttpResponseMessage> PostAsync(string url) => PostRawAsync(url, new StringContent(string.Empty));

    public async Task<HttpResponseMessage> LogoutAsync()
    {
        var response = await PostAsync("/auth/logout");
        // The session token is bound to the signed-in user. Sign-out makes that token invalid.
        _xsrf = null;
        if (response.StatusCode == HttpStatusCode.OK)
            await EnsureXsrfAsync();
        return response;
    }

    public async Task<HttpResponseMessage> RegisterAndSignInAsync(string name, string email, string? password = null)
    {
        var response = await PostJsonAsync(
            "/auth/register",
            new RegisterRequest(name, email, password ?? DefaultPassword),
            AppJsonSerializerContext.Default.RegisterRequest);

        var json = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Register failed ({(int)response.StatusCode}): {json}");

        var session = System.Text.Json.JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.AuthSessionResponse)
            ?? throw new InvalidOperationException("Register returned an empty body.");
        if (!string.IsNullOrEmpty(session.XsrfToken))
            _xsrf = session.XsrfToken;

        return response;
    }

    private async Task EnsureXsrfAsync()
    {
        if (_xsrf != null)
            return;

        var response = await _http.GetAsync("/auth/xsrf");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var text = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to fetch XSRF token ({(int)response.StatusCode}): {text}");
        }

        var body = await response.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.XsrfTokenResponse);
        if (string.IsNullOrEmpty(body?.Token))
            throw new InvalidOperationException("XSRF token response was empty.");

        _xsrf = body.Token;
    }
}
