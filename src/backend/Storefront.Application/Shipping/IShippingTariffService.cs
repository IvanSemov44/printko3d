namespace Storefront.Application.Shipping;

/// <summary>One step of the tariff: a parcel up to <paramref name="MaxWeightGrams"/> costs <paramref name="Price"/> (smallest currency unit).</summary>
public sealed record ShippingTariffStep(int MaxWeightGrams, long Price);

/// <summary>The tariff of delivery to the address, from the lightest step to the heaviest. No step means no delivery is offered.</summary>
public sealed record ShippingTariffDto(IReadOnlyList<ShippingTariffStep> Steps);

/// <summary>The tariff the test courier prices by (decision 0034), for the admin panel.</summary>
public interface IShippingTariffService
{
    Task<ShippingTariffDto> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces every step of the tariff in one transaction and returns the new tariff, sorted by weight. The steps may
    /// come in any order. An empty list switches the delivery off.
    /// Errors: 400 validation (too many steps, a weight or a price out of range, two steps of the same weight),
    /// 409 shipping.tariff_conflict (someone saved a tariff at the same moment).
    /// </summary>
    Task<ShippingTariffDto> ReplaceAsync(IReadOnlyList<ShippingTariffStep> steps, CancellationToken cancellationToken);
}
