using Storefront.Application.Carts.Queries;

namespace Storefront.Application.Carts;

/// <summary>
/// The cart after a change. <paramref name="Token"/> is the secret for the cookie: the new one when the cart was
/// just created, the shopper's own when it already existed, null when there is no cart.
/// </summary>
public sealed record CartChange(CartDto Cart, string? Token);
