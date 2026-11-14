using Storefront.Application.Catalog.Queries;
using Storefront.Application.Orders;
using Storefront.Domain.Orders;

namespace Storefront.Application.Contracts;

/// <summary>Read side of orders for the admin panel.</summary>
public interface IOrderQueries
{
    /// <summary>Every order, the newest first, one page at a time. The search matches the number exactly and the email and the name by a part.</summary>
    Task<PagedResult<OrderAdminListItemDto>> GetAdminOrdersAsync(OrderAdminListQuery query, CancellationToken cancellationToken);

    /// <summary>The number of orders of each status that has at least one order, with one query.</summary>
    Task<IReadOnlyDictionary<OrderStatus, int>> GetStatusCountsAsync(CancellationToken cancellationToken);
}
