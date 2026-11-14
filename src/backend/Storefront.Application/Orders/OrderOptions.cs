namespace Storefront.Application.Orders;

/// <summary>Settings of placing an order. Bound from the <c>Checkout</c> section.</summary>
public sealed class OrderOptions
{
    public const string SectionName = "Checkout";

    /// <summary>
    /// The version of the terms and conditions written to every order. Until the legal pages exist (task 32)
    /// this is a placeholder, so it is clear which orders were accepted under which text.
    /// </summary>
    public string TermsVersion { get; set; } = "draft-1";

    /// <summary>
    /// The address of the shop, for the links in the emails (for example <c>https://shop.example</c>). The default is the
    /// local address of Docker Compose; the real one is set per environment (task 33).
    /// </summary>
    public string StorefrontUrl { get; set; } = "http://localhost:5173";

    /// <summary>Requests a client may make to see an order in a minute (decision 0036, debt D64).</summary>
    public int LookupAttemptsPerMinute { get; set; } = 20;

    /// <summary>Orders a cart may place in a minute (decision 0036, debt D64). A cart is emptied by the first, so this only stops a script.</summary>
    public int PlaceAttemptsPerMinute { get; set; } = 10;

    /// <summary>Wrong emails for one order number before the number is closed for a while.</summary>
    public int LookupFailuresPerOrder { get; set; } = 5;

    /// <summary>Minutes a number stays closed after too many wrong emails, and the length of the count.</summary>
    public int LookupFailureWindowMinutes { get; set; } = 15;

    public static bool IsValid(OrderOptions options) =>
        options.TermsVersion.Length is > 0 and <= 50
        && Uri.TryCreate(options.StorefrontUrl, UriKind.Absolute, out var url)
        && url.Scheme is "http" or "https"
        && options.LookupAttemptsPerMinute is >= 1 and <= 1000
        && options.PlaceAttemptsPerMinute is >= 1 and <= 1000
        && options.LookupFailuresPerOrder is >= 1 and <= 100
        && options.LookupFailureWindowMinutes is >= 1 and <= 1440;
}
