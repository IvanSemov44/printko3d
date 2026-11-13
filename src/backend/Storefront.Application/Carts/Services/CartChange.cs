using Storefront.Application.Carts.Queries;

namespace Storefront.Application.Carts;

/// <summary>
/// The cart after a change. <paramref name="Token"/> is the secret for the cookie: the new one when the cart was
/// just created, the shopper's own when it already existed, null when there is no cart. <paramref name="LineCreated"/> is
/// true only when an add made a new line, not when it raised the units of a line that was already there.
/// </summary>
public sealed record CartChange(CartDto Cart, string? Token, bool LineCreated = false);
