using Storefront.Application.Carts.Queries;

namespace Storefront.Application.Contracts;

/// <summary>Read side of the cart: prices, names and states come from the variants at the moment of the read.</summary>
public interface ICartQueries
{
    /// <summary>The cart of the hash, at most <c>Cart.MaxItems</c> lines. An unknown hash gives an empty cart.</summary>
    Task<CartDto> GetAsync(byte[] tokenHash, CancellationToken cancellationToken);
}
