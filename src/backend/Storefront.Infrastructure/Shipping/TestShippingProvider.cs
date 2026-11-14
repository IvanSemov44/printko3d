using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Application.Shipping;

namespace Storefront.Infrastructure.Shipping;

/// <summary>
/// The test courier (decision 0034): no outside service, only the tariff in the database. It offers delivery to the
/// address, priced by the weight of the parcel. Nothing about the buyer is read or logged.
/// </summary>
internal sealed class TestShippingProvider(IRepositoryManager repositories) : IShippingProvider
{
    public async Task<IReadOnlyList<ShippingQuote>> GetQuotesAsync(
        ShippingRequest request,
        CancellationToken cancellationToken)
    {
        var steps = await repositories.ShippingRates.ListAsync(ShippingMethods.Address, cancellationToken);
        if (steps.Count == 0)
        {
            return [];
        }

        // The steps are ordered by weight, so the first one that covers the parcel is the cheapest that fits.
        var step = steps.FirstOrDefault(s => s.MaxWeightGrams >= request.WeightGrams)
            ?? throw new RuleViolationException(
                "The parcel is heavier than the delivery carries.", "shipping.too_heavy");
        return [new ShippingQuote(ShippingMethods.Address, step.Price)];
    }
}
