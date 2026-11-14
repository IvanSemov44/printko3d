namespace Storefront.Domain.Shipping;

/// <summary>
/// One step of the tariff of a delivery method: a parcel up to <see cref="MaxWeightGrams"/> costs <see cref="Price"/>.
/// The currency is the one of the store settings, so the tariff does not carry its own.
/// </summary>
public sealed class ShippingRate : Entity
{
    /// <summary>Stable code of the delivery method, for example <c>address</c>.</summary>
    public string MethodCode { get; set; } = string.Empty;

    /// <summary>The heaviest parcel this step covers, in grams. The first step that covers the weight is used.</summary>
    public int MaxWeightGrams { get; set; }

    /// <summary>Price in the smallest currency unit.</summary>
    public long Price { get; set; }
}
