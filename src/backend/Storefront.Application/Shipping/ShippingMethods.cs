namespace Storefront.Application.Shipping;

/// <summary>Codes of the delivery methods. The storefront shows the name of a method by its code (a translation).</summary>
public static class ShippingMethods
{
    /// <summary>The only country delivered to for now (ISO 3166-1 alpha-2).</summary>
    public const string Country = "BG";

    /// <summary>Delivery to the address of the buyer.</summary>
    public const string Address = "address";
}
