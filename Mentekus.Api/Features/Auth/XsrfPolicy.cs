namespace Mentekus.Api.Features.Auth;

public static class XsrfPolicy
{
    // Development skips the header so Scalar on the local launch profile can POST without copying a token.
    // Every other environment, including the test host, still requires it.
    public static bool RequiresValidation(string environmentName, string method)
    {
        if (string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
            return false;

        return HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);
    }
}
