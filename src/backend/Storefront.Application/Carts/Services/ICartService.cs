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

    /// <summary>
    /// Joins the cart of the cookie with the cart of the account when the buyer signs in (decision 0039), so there is one cart. Lines of
    /// the same variant are added together up to 20 units, and lines over 50 different items are left out. The cart of the cookie
    /// stays if there is one (its token is kept); when only the account has a cart, it gets a new token for the cookie. A cart of the
    /// cookie that belongs to another account is left alone. Nothing to join is not an error.
    /// </summary>
    Task<CartChange> MergeIntoAccountAsync(string? token, Guid customerId, CancellationToken cancellationToken);

    /// <summary>Empties the cart. No cart is an empty cart, not an error.</summary>
    Task<CartChange> ClearAsync(string? token, CancellationToken cancellationToken);
}
