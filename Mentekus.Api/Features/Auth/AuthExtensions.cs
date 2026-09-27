using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Mentekus.Api.Features.Auth;

public static class AuthExtensions
{
    public static IServiceCollection AddCookieAuth(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = AuthCookies.Session;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.Events.OnRedirectToLogin = context =>
                    WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized",
                        "Authentication is required.", "https://tools.ietf.org/html/rfc7235#section-3.1");
                options.Events.OnRedirectToAccessDenied = context =>
                    WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden, "Forbidden",
                        "You do not have access to this resource.", "https://tools.ietf.org/html/rfc7231#section-6.5.3");
            });

        services.AddAuthorization();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = AuthCookies.XsrfHeader;
            options.Cookie.Name = AuthCookies.Xsrf;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        return services;
    }

    public static WebApplication UseCookieAuth(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(ValidateXsrfAsync);
        return app;
    }

    private static async Task ValidateXsrfAsync(HttpContext context, RequestDelegate next)
    {
        var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
        if (XsrfPolicy.RequiresValidation(environment.EnvironmentName, context.Request.Method))
        {
            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                throw new ValidationException(AuthCookies.XsrfHeader, "XSRF token is missing or invalid.");
            }
        }

        await next(context);
    }

    private static Task WriteProblemAsync(HttpContext httpContext, int statusCode, string title, string detail, string type)
    {
        httpContext.Response.StatusCode = statusCode;
        var service = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        return service.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Type = type
            }
        }).AsTask();
    }
}
