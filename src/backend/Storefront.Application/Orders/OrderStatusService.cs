using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

internal sealed class OrderStatusService(IRepositoryManager repositories) : IOrderStatusService
{
    public async Task<OrderDto> ChangeAsync(int orderNumber, OrderStatus to, CancellationToken cancellationToken)
    {
        var order = await repositories.Order.GetByNumberAsync(orderNumber, cancellationToken)
            ?? throw new NotFoundException($"Order {orderNumber} was not found.", OrderErrorCodes.NotFound);

        if (!OrderTransitions.CanMove(order.Status, to))
        {
            throw Invalid(order.Status, to);
        }

        // The statement is conditional: if the payment or the expiry changed the status a moment ago, it changes nothing.
        if (!await repositories.Order.TryChangeStatusAsync(order.Id, order.Status, to, cancellationToken))
        {
            throw Invalid(order.Status, to);
        }

        order.Status = to;
        return OrderMapper.ToDto(order);
    }

    private static ConflictException Invalid(OrderStatus from, OrderStatus to) =>
        new($"An order cannot move from {from} to {to}.", OrderErrorCodes.StatusTransitionInvalid);
}
