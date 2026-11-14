using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;

namespace Storefront.Application.Orders;

/// <summary>A row of the history of orders of a buyer. Money is whole numbers in the smallest unit of <paramref name="Currency"/>.</summary>
public sealed record CustomerOrderListItemDto(
    int Number,
    string Status,
    string Currency,
    long Total,
    int ItemCount,
    DateTimeOffset CreatedAt);

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

internal sealed class CustomerOrderService(IRepositoryManager repositories) : ICustomerOrderService
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    public const int MaxPage = 10_000;

    public Task<PagedResult<CustomerOrderListItemDto>> ListAsync(
        Guid customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (page is < 1 or > MaxPage)
        {
            errors["page"] = [$"page must be between 1 and {MaxPage}."];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"pageSize must be between 1 and {MaxPageSize}."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException("One or more query parameters are invalid.", errors);
        }

        return repositories.OrderQueries.GetCustomerOrdersAsync(customerId, page, pageSize, cancellationToken);
    }

    public async Task<OrderDto> GetAsync(Guid customerId, int number, CancellationToken cancellationToken)
    {
        var order = await repositories.Order.GetByNumberAsync(number, cancellationToken);
        if (order is null || order.CustomerId != customerId)
        {
            throw new NotFoundException("The order was not found.", OrderErrorCodes.NotFound);
        }

        return OrderMapper.ToDto(order);
    }
}
