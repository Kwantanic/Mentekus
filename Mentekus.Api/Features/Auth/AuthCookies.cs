namespace Mentekus.Api.Features.Auth;

public static class AuthCookies
{
    public const string Session = "mentekus.auth";
    public const string Xsrf = "mentekus.xsrf";
    public const string XsrfHeader = "X-XSRF-TOKEN";
}
