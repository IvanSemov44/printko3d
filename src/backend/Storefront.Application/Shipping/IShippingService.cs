namespace Storefront.Application.Shipping;

/// <summary>
/// The delivery methods the buyer can choose for the cart and what each costs, in the smallest unit of the store
/// currency. <paramref name="ItemsTotal"/> is the total of the lines that can be ordered, without delivery; the total
/// of the order is it plus the amount of the chosen method.
/// </summary>
public sealed record ShippingOptionsDto(long ItemsTotal, IReadOnlyList<ShippingQuote> Methods);

public interface IShippingService
{
    /// <summary>
    /// The delivery methods for the cart of the cookie, with their prices. Never creates a cart.
    /// Errors: 422 order.cart_empty (no cart, an unknown cookie, or no line that can be ordered),
    /// 422 shipping.too_heavy (the cart is heavier than any delivery carries).
    /// </summary>
    Task<ShippingOptionsDto> GetOptionsAsync(string? cartToken, CancellationToken cancellationToken);
}
