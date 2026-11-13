namespace Storefront.Infrastructure.Email;

/// <summary>
/// The mail server (decision 0031). Every value comes from the environment (for example Email__Password); nothing is read
/// from a file in the repository. An empty <see cref="Host"/> means "no mail": messages are skipped.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>The SMTP server. Empty turns sending off.</summary>
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 587;

    /// <summary>Empty when the server needs no sign-in (the local catcher).</summary>
    public string User { get; init; } = string.Empty;

    /// <summary>A secret. It must never be logged or returned.</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>The sender address, from a domain of the shop.</summary>
    public string From { get; init; } = string.Empty;

    /// <summary>Encrypt the connection when the server offers it (STARTTLS on 587, TLS on 465). False for the local catcher.</summary>
    public bool UseTls { get; init; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);

    /// <summary>The names of the settings that are empty although a host is set. Only names, never values.</summary>
    public IReadOnlyList<string> MissingSettings() =>
        string.IsNullOrWhiteSpace(From) ? [$"{SectionName}:{nameof(From)}"] : [];
}
