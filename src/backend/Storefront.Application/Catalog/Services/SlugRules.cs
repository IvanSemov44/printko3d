using System.Text.RegularExpressions;

namespace Storefront.Application.Catalog;

/// <summary>The slug rules shared by categories and products: lower-case letters and digits, single hyphens between them.</summary>
internal static partial class SlugRules
{
    public const int MaxLength = 200;

    public const string FormatMessage = "Slug may contain only lower-case letters, digits and single hyphens between them.";

    public static bool IsValid(string slug) => slug.Length <= MaxLength && Format().IsMatch(slug);

    // \z, not $: "$" would also accept a trailing line break.
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex Format();
}
