namespace Storefront.Application.Shipping;

/// <summary>Used when no provider is registered: no delivery method is offered, so no order can be placed.</summary>
internal sealed class NoShippingProvider : IShippingProvider
{
    public Task<IReadOnlyList<ShippingQuote>> GetQuotesAsync(
        ShippingRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ShippingQuote>>([]);
}
