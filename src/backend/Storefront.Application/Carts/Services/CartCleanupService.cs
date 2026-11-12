using Storefront.Application.Contracts;

namespace Storefront.Application.Carts;

public sealed class CartCleanupService : ICartCleanupService
{
    public static readonly TimeSpan MaxIdle = TimeSpan.FromDays(30);

    public static readonly TimeSpan MaxIdleWhenEmpty = TimeSpan.FromDays(7);

    private readonly IRepositoryManager _repositories;
    private readonly TimeProvider _timeProvider;
    private readonly int _batchSize;

    public CartCleanupService(IRepositoryManager repositories, TimeProvider timeProvider, int batchSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        _repositories = repositories;
        _timeProvider = timeProvider;
        _batchSize = batchSize;
    }

    public async Task<int> DeleteStaleAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var deleted = await DeleteInBatchesAsync(now - MaxIdle, onlyEmpty: false, cancellationToken);
        return deleted + await DeleteInBatchesAsync(now - MaxIdleWhenEmpty, onlyEmpty: true, cancellationToken);
    }

    // A full batch means there may be more, so go on until a batch comes back short.
    private async Task<int> DeleteInBatchesAsync(DateTimeOffset cutoff, bool onlyEmpty, CancellationToken cancellationToken)
    {
        var total = 0;
        int deleted;
        do
        {
            deleted = await _repositories.Cart.DeleteStaleAsync(cutoff, onlyEmpty, _batchSize, cancellationToken);
            total += deleted;
        }
        while (deleted == _batchSize);

        return total;
    }
}
