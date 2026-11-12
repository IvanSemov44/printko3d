namespace Storefront.Application.Exceptions;

/// <summary>
/// A request is well formed but breaks a business limit (for example too many lines in a cart).
/// <see cref="Code"/> is a stable machine-readable code for API clients.
/// </summary>
public sealed class RuleViolationException(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
}
