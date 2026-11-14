namespace Storefront.Application.Shipping;

/// <summary>
/// What a courier needs to price a parcel. The weight is the sum of the lines; the address is where it goes. The city
/// and the postal code are null while the buyer has not typed them yet (the price shown on the checkout page).
/// </summary>
public sealed record ShippingRequest(long WeightGrams, string Country, string? City = null, string? PostalCode = null);

/// <summary>A way to deliver the parcel and its price in the smallest unit of the store currency.</summary>
public sealed record ShippingQuote(string Code, long Amount);

/// <summary>
/// Prices a delivery (decision 0034). The test courier reads a tariff from the database; a real courier replaces it
/// without a change in the order service or the storefront.
/// </summary>
public interface IShippingProvider
{
    /// <summary>
    /// The methods that can deliver the parcel, with their prices. The list is empty when no method is set up.
    /// Throws <see cref="Exceptions.RuleViolationException"/> with <c>shipping.too_heavy</c> when a method exists but
    /// the parcel is heavier than it carries; a price is never invented.
    /// </summary>
    Task<IReadOnlyList<ShippingQuote>> GetQuotesAsync(ShippingRequest request, CancellationToken cancellationToken);
}
