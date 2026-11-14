using Storefront.Application.Contracts;
using Storefront.Application.Emails;
using Storefront.Application.Exceptions;
using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

internal sealed class OrderStatusService(IRepositoryManager repositories, OrderOptions options) : IOrderStatusService
{
    public async Task<OrderDto> ChangeAsync(int orderNumber, OrderStatus to, CancellationToken cancellationToken)
    {
        var order = await repositories.Order.GetByNumberAsync(orderNumber, cancellationToken)
            ?? throw new NotFoundException($"Order {orderNumber} was not found.", OrderErrorCodes.NotFound);

        if (!OrderTransitions.CanMove(order.Status, to))
        {
            throw Invalid(order.Status, to);
        }

        // The change and the email are one transaction: a shipped order always has its email, and a refused move has none.
        await using var transaction = await repositories.BeginTransactionAsync(cancellationToken);

        // The statement is conditional: if the payment or the expiry changed the status a moment ago, it changes nothing.
        if (!await repositories.Order.TryChangeStatusAsync(order.Id, order.Status, to, cancellationToken))
        {
            throw Invalid(order.Status, to);
        }

        if (to == OrderStatus.Shipped)
        {
            repositories.OutgoingEmails.Enqueue(OrderEmails.Shipped(order, options.StorefrontUrl));
            await repositories.SaveAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        order.Status = to;
        return OrderMapper.ToDto(order);
    }

    private static ConflictException Invalid(OrderStatus from, OrderStatus to) =>
        new($"An order cannot move from {from} to {to}.", OrderErrorCodes.StatusTransitionInvalid);
}
