namespace Mentekus.Api.Infrastructure.ErrorHandling.Exceptions;

public class EmbeddingFailedException(string message, Exception? inner = null) : Exception(message, inner);
