namespace Storefront.Application.Auth;

/// <summary>An administrator account could not be created. Carries codes only, never the email or the password.</summary>
public sealed class AdminAccountException(IReadOnlyList<string> codes)
    : Exception($"The administrator account was refused: {string.Join(", ", codes)}.")
{
    public IReadOnlyList<string> Codes { get; } = codes;
}
