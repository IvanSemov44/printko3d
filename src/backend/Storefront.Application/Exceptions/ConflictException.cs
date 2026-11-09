namespace Storefront.Application.Exceptions;

/// <summary>The request clashes with the current state (a taken slug, a record changed by someone else). <see cref="Code"/> is stable for API clients.</summary>
public sealed class ConflictException(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
}
