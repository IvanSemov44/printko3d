using Storefront.Domain.Carts;

namespace Storefront.Application.Contracts;

public interface ICartRepository
{
    void Create(Cart cart);

    /// <summary>Finds the cart by the SHA-256 hash of the cookie token, with its lines. Null when there is none.</summary>
    Task<Cart?> GetByTokenHashAsync(byte[] tokenHash, bool trackChanges, CancellationToken cancellationToken);

    /// <summary>Can the variant be put in a cart? It must exist, be active and belong to a published product.</summary>
    Task<bool> IsVariantOrderableAsync(Guid variantId, CancellationToken cancellationToken);

    /// <summary>Marks the line as new for the next save.</summary>
    void AddItem(CartItem item);

    /// <summary>Marks the line for deletion at the next save.</summary>
    void RemoveItem(CartItem item);

    /// <summary>Marks every line of the cart for deletion at the next save.</summary>
    void RemoveItems(Cart cart);

    /// <summary>Makes the next save refresh <c>UpdatedAt</c> of the cart, because changing a line does not change the cart row.</summary>
    void Touch(Cart cart);

    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> carts last changed before <paramref name="cutoff"/> (only the empty ones when
    /// <paramref name="onlyEmpty"/>), with their lines, and returns how many it deleted. A cart changed while the batch
    /// is deleted is kept.
    /// </summary>
    Task<int> DeleteStaleAsync(DateTimeOffset cutoff, bool onlyEmpty, int batchSize, CancellationToken cancellationToken);
}
