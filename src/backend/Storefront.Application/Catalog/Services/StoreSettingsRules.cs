using System.Text.RegularExpressions;

namespace Storefront.Application.Catalog;

/// <summary>
/// What the settings may contain. The lists of allowed values are validation rules, not settings: the values
/// that are chosen from them live in the table (CLAUDE.md).
/// </summary>
internal static partial class StoreSettingsRules
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 320;
    public const int PhoneMaxLength = 50;

    /// <summary>The primary colour is a frame and a button background, so it must stand out from white (WCAG 2.1, 1.4.11).</summary>
    public const double MinColorContrast = 3.0;

    public const string White = "#ffffff";

    /// <summary>Allowed currencies with the number of digits after the decimal point. Prices are whole numbers in the smallest unit.</summary>
    public static IReadOnlyDictionary<string, int> DefaultCurrencies { get; } = new Dictionary<string, int>
    {
        ["EUR"] = 2,
        ["BGN"] = 2,
    };

    /// <summary>Only languages that have translation files in the storefront.</summary>
    public static IReadOnlySet<string> Locales { get; } = new HashSet<string> { "bg" };

    public static bool IsEmail(string value) => EmailPattern().IsMatch(value);

    public static bool IsPhone(string value) => PhonePattern().IsMatch(value);

    public static bool IsHexColor(string value) => ColorPattern().IsMatch(value);

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^[0-9 +\-()]+$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorPattern();
}
