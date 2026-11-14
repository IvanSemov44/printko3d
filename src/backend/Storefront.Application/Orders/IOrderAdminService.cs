using Storefront.Application.Catalog.Queries;
using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

/// <summary>The administrator's view of orders and the steps the administrator may take (task 28, decision 0037).</summary>
public interface IOrderAdminService
{
    /// <summary>A page of orders, the newest first, filtered by status and a search by number, email or name.</summary>
    Task<PagedResult<OrderAdminListItemDto>> GetListAsync(OrderAdminListQuery query, CancellationToken cancellationToken);

    /// <summary>The number of orders of every status, zero for a status without orders, in the order of the statuses.</summary>
    Task<IReadOnlyList<OrderStatusCountDto>> GetStatusCountsAsync(CancellationToken cancellationToken);

    /// <summary>One order with its lines. Errors: 404 order.not_found.</summary>
    Task<OrderAdminDto> GetAsync(int number, CancellationToken cancellationToken);

    /// <summary>
    /// One production step forward (<c>OrderTransitions.AdminMoves</c>). Payment and refusal are not steps here. Errors:
    /// 404 order.not_found, 409 order.status_transition_invalid.
    /// </summary>
    Task<OrderAdminDto> ChangeStatusAsync(int number, OrderStatus to, CancellationToken cancellationToken);

    /// <summary>
    /// Refuses the order from <c>New</c>, <c>Paid</c>, <c>InProduction</c> or <c>ReadyToShip</c> and gives the stock back.
    /// Errors: 404 order.not_found, 409 order.status_transition_invalid.
    /// </summary>
    Task<OrderCancelledDto> CancelAsync(int number, CancellationToken cancellationToken);
}
