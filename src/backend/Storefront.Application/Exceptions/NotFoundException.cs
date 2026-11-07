namespace Storefront.Application.Exceptions;

/// <summary>A resource does not exist. <see cref="Code"/> is a stable machine-readable code for API clients.</summary>
public sealed class NotFoundException(string message, string code = "resource.not_found") : Exception(message)
{
    public string Code { get; } = code;
}
