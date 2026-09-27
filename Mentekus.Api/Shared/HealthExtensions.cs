namespace Mentekus.Api.Shared;

public static class HealthExtensions
{
    public static WebApplication MapHealth(this WebApplication app)
    {
        app.MapGet("/health", () => TypedResults.Text("ok"));
        return app;
    }
}
