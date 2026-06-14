using System.Diagnostics;
using System.Text.Json;
using Mentekus.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Mentekus.Api.Infrastructure.ErrorHandling;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        var statusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            JsonException => StatusCodes.Status400BadRequest,
            BadHttpRequestException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = exception switch
        {
            ValidationException validationException => new ValidationProblemDetails(validationException.Errors ?? new Dictionary<string, string[]>())
            {
                Status = statusCode,
                Title = "Validation Error",
                Detail = validationException.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            },
            JsonException jsonException => new ProblemDetails
            {
                Status = statusCode,
                Title = "Bad Request",
                Detail = GetJsonExceptionDetail(jsonException),
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            },
            BadHttpRequestException badHttpRequestException => new ProblemDetails
            {
                Status = statusCode,
                Title = "Bad Request",
                Detail = (badHttpRequestException.InnerException is JsonException jsonEx)
                    ? GetJsonExceptionDetail(jsonEx)
                    : badHttpRequestException.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            },
            _ => new ProblemDetails
            {
                Status = statusCode,
                Title = "An error occurred",
                Detail = "An unexpected error occurred. Please try again later.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            }
        };

        if (exception is JsonException && logger.IsEnabled(LogLevel.Debug))
        {
            problemDetails.Detail += $" {exception.Message}";
        }

        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    private static string GetJsonExceptionDetail(JsonException exception)
    {
        var message = exception.Message;

        // Handle missing required properties (STJ format)
        // Message usually looks like: JSON deserialization for type '...' was missing required properties including: 'propertyName'.
        if (message.Contains("missing required properties including:", StringComparison.OrdinalIgnoreCase))
        {
            var match = System.Text.RegularExpressions.Regex.Match(message, @"including: '([^']+)'", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var propertyName = match.Groups[1].Value;
                return $"request.{propertyName} is required";
            }
        }

        // Handle malformed JSON with path
        // Message usually looks like: '...' is an invalid start of a value. Path: $.propertyName | LineNumber: ...
        if (exception.Path is not null && exception.Path.StartsWith("$."))
        {
            var propertyName = exception.Path[2..];
            // If it's a simple property (no nested path)
            if (!propertyName.Contains('.') && !propertyName.Contains('['))
            {
                return $"request.{propertyName} is malformed or missing";
            }
        }

        return $"The request contains malformed JSON: {message}";
    }
}
