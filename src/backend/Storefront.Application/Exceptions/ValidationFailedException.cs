namespace Storefront.Application.Exceptions;

public sealed class ValidationFailedException(string message, IReadOnlyDictionary<string, string[]>? errors = null)
    : Exception(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}
