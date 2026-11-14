using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

/// <summary>The order after a refusal, and the status it had just before.</summary>
public sealed record OrderCancellation(OrderDto Order, OrderStatus Was);

public interface IOrderStatusService
{
    /// <summary>
    /// Moves the order to <paramref name="to"/> if <c>OrderTransitions</c> allows it from the current status. A move to
    /// <c>Cancelled</c> is a refusal (<see cref="CancelAsync"/>), so the stock never stays taken. The administrator reaches
    /// this through <see cref="IOrderAdminService"/>. Errors: 404 order.not_found,
    /// 409 order.status_transition_invalid (the move is not allowed, or the order changed at the same moment).
    /// </summary>
    Task<OrderDto> ChangeAsync(int orderNumber, OrderStatus to, CancellationToken cancellationToken);

    /// <summary>
    /// Refuses the order and gives its stock back, in one transaction. Two refusals, or a refusal and a payment, at the same
    /// moment have one winner and the stock goes back once. Errors: 404 order.not_found, 409 order.status_transition_invalid
    /// (shipped, completed, or already cancelled).
    /// </summary>
    Task<OrderCancellation> CancelAsync(int orderNumber, CancellationToken cancellationToken);
}
