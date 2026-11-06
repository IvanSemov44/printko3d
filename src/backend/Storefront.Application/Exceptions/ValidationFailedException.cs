namespace Storefront.Application.Exceptions;

public sealed class ValidationFailedException(string message) : Exception(message);
