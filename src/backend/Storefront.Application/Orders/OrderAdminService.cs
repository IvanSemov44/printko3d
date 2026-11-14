using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

internal sealed class OrderAdminService(IRepositoryManager repositories, IOrderStatusService statuses) : IOrderAdminService
{
    public Task<PagedResult<OrderAdminListItemDto>> GetListAsync(OrderAdminListQuery query, CancellationToken cancellationToken) =>
        repositories.OrderQueries.GetAdminOrdersAsync(query, cancellationToken);

    public async Task<IReadOnlyList<OrderStatusCountDto>> GetStatusCountsAsync(CancellationToken cancellationToken)
    {
        var counts = await repositories.OrderQueries.GetStatusCountsAsync(cancellationToken);
        return Enum.GetValues<OrderStatus>()
            .Select(status => new OrderStatusCountDto(status.ToString(), counts.GetValueOrDefault(status)))
            .ToList();
    }

    public async Task<OrderAdminDto> GetAsync(int number, CancellationToken cancellationToken) =>
        ToAdminDto(await GetOrderAsync(number, cancellationToken));

    public async Task<OrderAdminDto> ChangeStatusAsync(int number, OrderStatus to, CancellationToken cancellationToken)
    {
        var order = await GetOrderAsync(number, cancellationToken);
        if (!OrderTransitions.AdminMoves(order.Status).Contains(to))
        {
            throw new ConflictException(
                $"The administrator cannot move an order from {order.Status} to {to}.",
                OrderErrorCodes.StatusTransitionInvalid);
        }

        await statuses.ChangeAsync(number, to, cancellationToken);
        return await GetAsync(number, cancellationToken);
    }

    public async Task<OrderCancelledDto> CancelAsync(int number, CancellationToken cancellationToken)
    {
        var cancellation = await statuses.CancelAsync(number, cancellationToken);
        return new OrderCancelledDto(
            await GetAsync(number, cancellationToken),
            RefundByHand: cancellation.Was != OrderStatus.New);
    }

    private async Task<Order> GetOrderAsync(int number, CancellationToken cancellationToken) =>
        await repositories.Order.GetByNumberAsync(number, cancellationToken)
            ?? throw new NotFoundException($"Order {number} was not found.", OrderErrorCodes.NotFound);

    private static OrderAdminDto ToAdminDto(Order order) => new(
        OrderMapper.ToDto(order),
        OrderTransitions.AdminMoves(order.Status).Select(s => s.ToString()).ToList(),
        OrderTransitions.CanMove(order.Status, OrderStatus.Cancelled));
}
