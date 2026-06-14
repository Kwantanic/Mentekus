namespace Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;

public class NotFoundException(string message) : Exception(message)
{
    public NotFoundException() : this("The requested resource was not found.")
    {
    }

    public NotFoundException(string name, object key) 
        : this($"Entity \"{name}\" ({key}) was not found.")
    {
    }
}
