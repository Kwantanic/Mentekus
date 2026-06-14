namespace Mentekus.Api.Shared.ErrorHandling;

public class ValidationException(string message, IDictionary<string, string[]>? errors = null) : Exception(message)
{
    public ValidationException(string propertyName, string errorMessage)
        : this($"{propertyName}: {errorMessage}", new Dictionary<string, string[]>
        {
            { propertyName, [errorMessage] }
        })
    {
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : this("One or more validation errors occurred.", errors)
    {
    }

    public IDictionary<string, string[]>? Errors { get; } = errors;
}