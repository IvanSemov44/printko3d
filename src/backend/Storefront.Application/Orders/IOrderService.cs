namespace Storefront.Application.Orders;

public interface IOrderService
{
    /// <summary>
    /// Turns the cart of the cookie into an order and empties the cart, in one save. Prices, names and the total
    /// come from the variants, never from the buyer.
    /// Errors: 400 validation (a field), 409 order.total_changed, 409 cart.changed (the cart changed while the order was made),
    /// 422 order.cart_empty, 422 order.cart_not_orderable (a line is archived, inactive or over the stock).
    /// </summary>
    Task<OrderDto> PlaceAsync(string? cartToken, PlaceOrderInput input, CancellationToken cancellationToken);
}
