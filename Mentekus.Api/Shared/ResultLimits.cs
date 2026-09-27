namespace Mentekus.Api.Shared;

internal static class ResultLimits
{
    public static int Clamp(int limit, int fallback) =>
        Math.Clamp(limit <= 0 ? fallback : limit, 1, 50);
}
