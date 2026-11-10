namespace Storefront.Application.Catalog;

/// <summary>
/// WCAG 2.1 contrast ratio between two <c>#rrggbb</c> colours. The same formula as <c>contrastRatio</c> in
/// <c>@storefront/shared</c>; a test with the same colours in both places keeps them from drifting apart.
/// </summary>
internal static class ColorContrast
{
    public static double Ratio(string first, string second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var red = ChannelLuminance(Convert.ToInt32(hex.Substring(1, 2), 16));
        var green = ChannelLuminance(Convert.ToInt32(hex.Substring(3, 2), 16));
        var blue = ChannelLuminance(Convert.ToInt32(hex.Substring(5, 2), 16));
        return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
    }

    private static double ChannelLuminance(int channel)
    {
        var value = channel / 255.0;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
