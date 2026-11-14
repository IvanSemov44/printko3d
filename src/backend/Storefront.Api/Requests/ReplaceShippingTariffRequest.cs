using System.ComponentModel.DataAnnotations;
using Storefront.Application.Shipping;

namespace Storefront.Api.Requests;

/// <summary>The body of the request that replaces the tariff of delivery to the address. An empty list switches the delivery off.</summary>
/// <param name="Steps">The steps, in any order, at most 20.</param>
public sealed record ReplaceShippingTariffRequest([Required, MaxLength(20)] IReadOnlyList<ShippingStepRequest> Steps)
{
    public IReadOnlyList<ShippingTariffStep> ToModel() =>
        Steps.Select(step => new ShippingTariffStep(step.MaxWeightGrams, step.Price)).ToList();
}

/// <summary>A parcel up to <paramref name="MaxWeightGrams"/> costs <paramref name="Price"/> in the smallest unit of the store currency.</summary>
public sealed record ShippingStepRequest(
    [Range(1, 1_000_000)] int MaxWeightGrams,
    [Range(0, 100_000_000)] long Price);
