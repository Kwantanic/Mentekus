namespace Mentekus.Api.Tests.Integration;

// HttpClient's cookie container drops SameSite=Lax cookies on POST. The auth and XSRF cookies are Lax,
// so tests keep the jar themselves and send every cookie the server set.
internal sealed class CookieJarHandler : DelegatingHandler
{
    private readonly Dictionary<string, string> _cookies = new(StringComparer.OrdinalIgnoreCase);

    public CookieJarHandler(HttpMessageHandler inner) : base(inner)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_cookies.Count > 0)
        {
            request.Headers.Remove("Cookie");
            request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", _cookies.Select(pair => $"{pair.Key}={pair.Value}")));
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var setCookie in setCookies)
                Apply(setCookie);
        }

        return response;
    }

    private void Apply(string setCookie)
    {
        var parts = setCookie.Split(';');
        var eq = parts[0].IndexOf('=');
        if (eq <= 0)
            return;

        var name = parts[0][..eq].Trim();
        var value = parts[0][(eq + 1)..].Trim();
        if (Expired(parts))
            _cookies.Remove(name);
        else
            _cookies[name] = value;
    }

    private static bool Expired(string[] parts)
    {
        foreach (var part in parts.Skip(1))
        {
            var piece = part.Trim();
            if (piece.StartsWith("max-age=", StringComparison.OrdinalIgnoreCase))
                return piece["max-age=".Length..] == "0";

            if (piece.StartsWith("expires=", StringComparison.OrdinalIgnoreCase)
                && DateTimeOffset.TryParse(piece["expires=".Length..], out var expires))
                return expires <= DateTimeOffset.UtcNow;
        }

        return false;
    }
}
