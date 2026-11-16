using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;

namespace Storefront.Application.Orders;

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
