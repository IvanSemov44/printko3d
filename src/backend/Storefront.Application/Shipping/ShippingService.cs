using Storefront.Application.Carts;
using Storefront.Application.Carts.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;

namespace Storefront.Application.Shipping;

internal sealed class ShippingService(IRepositoryManager repositories, IShippingProvider provider) : IShippingService
{
    public async Task<ShippingOptionsDto> GetOptionsAsync(string? cartToken, CancellationToken cancellationToken)
    {
        var cart = CartToken.TryHash(cartToken, out var hash)
            ? await repositories.CartQueries.GetAsync(hash, cancellationToken)
            : CartDto.Empty;

        // The same lines that make up the total of the cart: a line that cannot be ordered is neither paid nor weighed.
        var lines = cart.Lines.Where(line => line.State == CartLineState.Available).ToList();
        if (lines.Count == 0)
        {
            throw new RuleViolationException("The cart is empty.", "order.cart_empty");
        }

        var quotes = await provider.GetQuotesAsync(
            new ShippingRequest(ParcelWeight.Of(lines), ShippingMethods.Country), cancellationToken);
        return new ShippingOptionsDto(cart.Total, quotes);
    }
}

/// <summary>The weight of a parcel is the sum of the weight of every unit in the lines.</summary>
internal static class ParcelWeight
{
    public static long Of(IEnumerable<CartLineDto> lines) => lines.Sum(line => (long)line.WeightGrams * line.Quantity);
}
