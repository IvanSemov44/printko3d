namespace Storefront.Api.Setup;

/// <summary>The names of the limits for the open addresses of orders (decision 0036, debt D64).</summary>
public static class OrderRateLimits
{
    /// <summary>Finding an order by number and email or by the token of the link.</summary>
    public const string Lookup = "order-lookup";

    /// <summary>Placing an order.</summary>
    public const string Place = "order-place";
}
