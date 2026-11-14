namespace Storefront.Application.Orders;

public interface IOrderService
{
    /// <summary>
    /// Turns the cart of the cookie into an order and empties the cart, in one transaction that also takes the stock of
    /// the lines that are in stock. Prices, names and the total come from the variants, never from the buyer.
    /// Errors: 400 validation (a field), 409 order.total_changed, 409 cart.changed (the cart changed while the order was made),
    /// 409 order.stock_changed (someone took the last pieces between the check and the save),
    /// 422 order.cart_empty, 422 order.cart_not_orderable (a line is archived, inactive or over the stock).
    /// </summary>
    Task<OrderDto> PlaceAsync(string? cartToken, PlaceOrderInput input, CancellationToken cancellationToken);

    /// <summary>
    /// Gives the stock of the order back, once: a second call does nothing. Lines made to order have no stock and are
    /// skipped. It is called when an order is refused or its payment time runs out (tasks 26 and 28); there is no
    /// public address for it. Error: 404 order.not_found.
    /// </summary>
    Task ReleaseStockAsync(Guid orderId, CancellationToken cancellationToken);
}
