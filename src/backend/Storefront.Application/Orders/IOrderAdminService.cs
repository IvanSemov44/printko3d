using Storefront.Application.Catalog.Queries;

namespace Storefront.Application.Orders;

/// <summary>The administrator's view of orders (task 28).</summary>
public interface IOrderAdminService
{
    /// <summary>A page of orders, the newest first, filtered by status and a search by number, email or name.</summary>
    Task<PagedResult<OrderAdminListItemDto>> GetListAsync(OrderAdminListQuery query, CancellationToken cancellationToken);

    /// <summary>One order with its lines. Errors: 404 order.not_found.</summary>
    Task<OrderAdminDto> GetAsync(int number, CancellationToken cancellationToken);
}
