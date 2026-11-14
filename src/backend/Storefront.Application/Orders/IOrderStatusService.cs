using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

public interface IOrderStatusService
{
    /// <summary>
    /// Moves the order to <paramref name="to"/> if <c>OrderTransitions</c> allows it from the current status. There is no
    /// public address for it: the administrator gets one in task 28. Errors: 404 order.not_found,
    /// 409 order.status_transition_invalid (the move is not allowed, or the order changed at the same moment).
    /// </summary>
    Task<OrderDto> ChangeAsync(int orderNumber, OrderStatus to, CancellationToken cancellationToken);
}
