namespace Storefront.Application.Carts;

/// <summary>Deletes the carts nobody uses any more, so the tables do not grow for ever (decision 0027).</summary>
public interface ICartCleanupService
{
    /// <summary>
    /// Deletes carts with no change for 30 days, and empty carts with no change for 7 days.
    /// Works in batches, each one its own short transaction. Returns how many carts it deleted.
    /// </summary>
    Task<int> DeleteStaleAsync(CancellationToken cancellationToken);
}
