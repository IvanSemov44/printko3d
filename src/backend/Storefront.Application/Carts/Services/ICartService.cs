using Storefront.Application.Carts.Queries;

namespace Storefront.Application.Carts;

/// <summary>
/// The anonymous cart (decision 0027). Every method takes the raw cookie value, or null when there is none.
/// An unknown or malformed value is never an error: it is an empty cart.
/// </summary>
public interface ICartService
{
    /// <summary>Reads the cart. Never creates one.</summary>
    Task<CartDto> GetAsync(string? token, CancellationToken cancellationToken);

    /// <summary>One line of the cart. Errors: 404 cart.item_not_found (also when there is no cart).</summary>
    Task<CartLineDto> GetItemAsync(string? token, Guid variantId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds units of a variant; units of a variant already in the cart are added to its line. Creates the cart on the first add.
    /// Errors: 400 validation (quantity below 1), 404 variant.not_found, 422 cart.too_many_items, 422 cart.quantity_too_high,
    /// 409 cart.item_conflict.
    /// </summary>
    Task<CartChange> AddItemAsync(string? token, Guid variantId, int quantity, CancellationToken cancellationToken);

    /// <summary>Errors: 400 validation, 404 cart.item_not_found, 422 cart.quantity_too_high.</summary>
    Task<CartChange> SetQuantityAsync(string? token, Guid variantId, int quantity, CancellationToken cancellationToken);

    /// <summary>Errors: 404 cart.item_not_found.</summary>
    Task<CartChange> RemoveItemAsync(string? token, Guid variantId, CancellationToken cancellationToken);

    /// <summary>Empties the cart. No cart is an empty cart, not an error.</summary>
    Task<CartChange> ClearAsync(string? token, CancellationToken cancellationToken);
}
