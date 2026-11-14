using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

internal sealed class OrderAdminService(IRepositoryManager repositories) : IOrderAdminService
{
    public Task<PagedResult<OrderAdminListItemDto>> GetListAsync(OrderAdminListQuery query, CancellationToken cancellationToken) =>
        repositories.OrderQueries.GetAdminOrdersAsync(query, cancellationToken);

    public async Task<OrderAdminDto> GetAsync(int number, CancellationToken cancellationToken)
    {
        var order = await repositories.Order.GetByNumberAsync(number, cancellationToken)
            ?? throw new NotFoundException($"Order {number} was not found.", OrderErrorCodes.NotFound);

        return new OrderAdminDto(
            OrderMapper.ToDto(order),
            OrderTransitions.AdminMoves(order.Status).Select(s => s.ToString()).ToList());
    }
}
