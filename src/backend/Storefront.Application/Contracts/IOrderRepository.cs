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

    /// <summary>The order with its lines, by the number the buyer sees, or null. Not tracked.</summary>
    Task<Order?> GetByNumberAsync(int number, CancellationToken cancellationToken);

    /// <summary>
    /// <c>WHERE status = New</c> to <c>Paid</c>, in one statement. Returns false when the order was not new any more
    /// (paid, or cancelled by the expiry at the same moment), so only one of two competing changes wins. It does not
    /// wait for a save.
    /// </summary>
    Task<bool> TryMarkPaidAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// <c>WHERE status = Cancelled</c> to <c>Paid</c>, and the mark that the stock was given back is cleared, in one
    /// statement. The caller has already taken the stock again. Returns false when the order was not cancelled. It does
    /// not wait for a save.
    /// </summary>
    Task<bool> TryReviveAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// The orders that are still new and whose deadline passed before <paramref name="cutoff"/>, the oldest first, with
    /// their lines. At most <paramref name="batchSize"/>. Not tracked.
    /// </summary>
    Task<IReadOnlyList<Order>> ListExpiredAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken);

    /// <summary>
    /// <c>WHERE status = New AND payment_due_at &lt; cutoff</c> to <c>Cancelled</c>, in one statement, so a payment that
    /// arrives at the same moment and an expiry cannot both win. Returns false when the order was not new any more or
    /// its deadline was moved. It does not wait for a save.
    /// </summary>
    Task<bool> TryCancelExpiredAsync(Guid orderId, DateTimeOffset cutoff, CancellationToken cancellationToken);
}
