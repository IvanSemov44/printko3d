using Storefront.Domain.Orders;

namespace Storefront.Application.Contracts;

public interface IOrderRepository
{
    /// <summary>Marks the order, with its lines, as new for the next save.</summary>
    void Create(Order order);

    Task<bool> NumberExistsAsync(int number, CancellationToken cancellationToken);
}
