namespace Storefront.Api.Auth;

/// <summary>
/// The keys of the social sign-in providers (decision 0039), section <c>Authentication</c>. They are secrets of the environment, never in the
/// repository. A provider with a key missing is turned off: its button is not shown and its address answers 404.
/// </summary>
public sealed class ExternalLoginOptions
{
    public const string SectionName = "Authentication";

    /// <summary>The names of the schemes. They are also what the database keeps as the provider of a connected account.</summary>
    public const string GoogleName = "Google";

    public const string FacebookName = "Facebook";

    public GoogleKeys Google { get; set; } = new();

    public FacebookKeys Facebook { get; set; } = new();

    public sealed class GoogleKeys
    {
        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        public bool IsSet => ClientId.Length > 0 && ClientSecret.Length > 0;
    }

    public sealed class FacebookKeys
    {
        public string AppId { get; set; } = string.Empty;

        public string AppSecret { get; set; } = string.Empty;

        public bool IsSet => AppId.Length > 0 && AppSecret.Length > 0;
    }
}

/// <summary>The providers that are turned on in this environment, by the name of their scheme.</summary>
public sealed record EnabledExternalProviders(IReadOnlyList<string> Names)
{
    /// <summary>The name of the scheme for a provider typed in an address, in any case, or null when the provider is not turned on.</summary>
    public string? Find(string provider) =>
        Names.FirstOrDefault(name => string.Equals(name, provider, StringComparison.OrdinalIgnoreCase));
}
