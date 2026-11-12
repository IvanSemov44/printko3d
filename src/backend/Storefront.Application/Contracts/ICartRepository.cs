using Storefront.Domain.Carts;

namespace Storefront.Application.Contracts;

public interface ICartRepository
{
    void Create(Cart cart);

    /// <summary>Finds the cart by the SHA-256 hash of the cookie token, with its lines. Null when there is none.</summary>
    Task<Cart?> GetByTokenHashAsync(byte[] tokenHash, bool trackChanges, CancellationToken cancellationToken);

    /// <summary>Marks the line for deletion at the next save.</summary>
    void RemoveItem(CartItem item);

    /// <summary>Marks every line of the cart for deletion at the next save.</summary>
    void RemoveItems(Cart cart);
}
