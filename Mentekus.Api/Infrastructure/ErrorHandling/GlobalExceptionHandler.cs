using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Mentekus.Api.Infrastructure.ErrorHandling;

public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            JsonException => StatusCodes.Status400BadRequest,
            BadHttpRequestException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            ForbiddenException => StatusCodes.Status403Forbidden,
            NotFoundException => StatusCodes.Status404NotFound,
            EmbeddingFailedException => StatusCodes.Status503ServiceUnavailable,
            OperationCanceledException => 499, // Client Closed Request
            _ => StatusCodes.Status500InternalServerError
        };

        if (statusCode == StatusCodes.Status503ServiceUnavailable)
            logger.LogWarning(exception, "Embedding request failed: {Message}", exception.Message);
        else if (statusCode >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Request failed: {Message}", exception.Message);
        else
            logger.LogInformation("Request rejected with status {StatusCode}: {Message}", statusCode, exception.Message);

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = exception switch
        {
            ValidationException validationException => new ValidationProblemDetails(validationException.Errors ??
                new Dictionary<string, string[]>())
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
                Detail = badHttpRequestException.InnerException is JsonException jsonEx
                    ? GetJsonExceptionDetail(jsonEx)
                    : badHttpRequestException.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            },
            ArgumentException argumentException => new ProblemDetails
            {
                Status = statusCode,
                Title = "Bad Request",
                Detail = argumentException.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            },
            UnauthorizedAccessException unauthorizedAccessException => new ProblemDetails
            {
                Status = statusCode,
                Title = "Unauthorized",
                Detail = unauthorizedAccessException.Message,
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
            },
            ForbiddenException forbiddenException => new ProblemDetails
            {
                Status = statusCode,
                Title = "Forbidden",
                Detail = forbiddenException.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3"
            },
            NotFoundException notFoundException => new ProblemDetails
            {
                Status = statusCode,
                Title = "Not Found",
                Detail = notFoundException.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4"
            },
            EmbeddingFailedException embeddingFailedException => new ProblemDetails
            {
                Status = statusCode,
                Title = "Service Unavailable",
                Detail = embeddingFailedException.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.4"
            },
            OperationCanceledException => new ProblemDetails
            {
                Status = statusCode,
                Title = "Client Closed Request",
                Detail = "The request was canceled by the client.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5"
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
            problemDetails.Detail += $" {exception.Message}";

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
            var match = Regex.Match(message, @"including: '([^']+)'", RegexOptions.IgnoreCase);
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
                return $"request.{propertyName} is malformed or missing";
        }

        return $"The request contains malformed JSON: {message}";
    }
}