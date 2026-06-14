namespace Mentekus.Api.Shared.ErrorHandling;

public class ValidationException(string message) : Exception(message)
{
    public ValidationException(string propertyName, string errorMessage) 
        : this($"{propertyName}: {errorMessage}")
    {
        Errors = new Dictionary<string, string[]>
        {
            { propertyName, [errorMessage] }
        };
    }

    public IDictionary<string, string[]>? Errors { get; }
}
