using Storefront.Domain.Orders;

namespace Storefront.Application.Contracts;

public interface IOrderRepository
{
    /// <summary>Marks the order, with its lines, as new for the next save.</summary>
    void Create(Order order);

    Task<bool> NumberExistsAsync(int number, CancellationToken cancellationToken);

    /// <summary>The order with its lines, or null.</summary>
    Task<Order?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);

    /// <summary>
    /// Records that the stock of the order was given back, in one conditional statement
    /// (<c>WHERE stock_released_at IS NULL</c>). Returns false when it was already recorded, so the caller must not
    /// give the stock back a second time. It does not wait for a save.
    /// </summary>
    Task<bool> TryMarkStockReleasedAsync(Guid orderId, CancellationToken cancellationToken);
}
