namespace Storefront.Application.Exceptions;

/// <summary>An uploaded file is not acceptable (wrong type, empty, too big). <see cref="Code"/> is stable for API clients.</summary>
public sealed class FileRejectedException(string message, string code, bool tooLarge = false) : Exception(message)
{
    public string Code { get; } = code;

    /// <summary>True when the file is over the size limit, which is a different HTTP status from the other reasons.</summary>
    public bool TooLarge { get; } = tooLarge;
}
