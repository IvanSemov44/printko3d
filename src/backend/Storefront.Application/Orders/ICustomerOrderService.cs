using Storefront.Application.Catalog.Queries;

namespace Storefront.Application.Orders;

/// <summary>The orders of a signed-in buyer (decision 0039). Only the orders of the account are ever shown.</summary>
public interface ICustomerOrderService
{
    /// <summary>
    /// The orders of the account, the newest first, one page at a time. Errors: 400 validation.failed (page or pageSize out of
    /// range).
    /// </summary>
    Task<PagedResult<CustomerOrderListItemDto>> ListAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// One order of the account. The same answer for a number that does not exist and one that belongs to someone else, so
    /// the call does not tell which numbers exist. Errors: 404 order.not_found.
    /// </summary>
    Task<OrderDto> GetAsync(Guid customerId, int number, CancellationToken cancellationToken);
}
