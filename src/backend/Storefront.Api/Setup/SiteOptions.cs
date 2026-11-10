namespace Storefront.Api.Setup;

/// <summary>Where the shop is reachable for visitors. Needed for the absolute addresses in sitemap.xml.</summary>
public sealed class SiteOptions
{
    public const string SectionName = "Site";

    /// <summary>For example https://shop.example. Empty when the environment has not set it: there is then no sitemap.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Empty is allowed; anything else has to be a plain http(s) address.</summary>
    public static bool IsValid(SiteOptions options) =>
        options.Url == string.Empty
        || (Uri.TryCreate(options.Url, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https"
            && uri.AbsolutePath == "/"
            && uri.Query == string.Empty
            && uri.Fragment == string.Empty);
}
