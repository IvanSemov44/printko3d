using System.Data.Common;
using Storefront.Application.Contracts;
using Storefront.Application.Orders;
using Storefront.Domain.Orders;

namespace Storefront.Application.Payments;

public sealed class PaymentExpiryService(
    IRepositoryManager repositories,
    TimeProvider timeProvider,
    PaymentOptions options) : IPaymentExpiryService
{
    private enum Attempt
    {
        Cancelled,
        Skipped,
        Failed,
    }

    public async Task<PaymentExpiryReport> CancelExpiredAsync(CancellationToken cancellationToken)
    {
        // The pause after the deadline lets a payment that was started before it arrive first.
        var cutoff = timeProvider.GetUtcNow().AddMinutes(-options.GraceMinutes);
        var cancelled = 0;
        var failed = 0;
        var attempted = new HashSet<Guid>();

        // A full batch means there may be more. An order that was tried is not tried again in this run, so an order
        // that keeps failing cannot hold the run in a loop; the next run tries it again.
        List<Order> batch;
        do
        {
            batch = (await repositories.Order.ListExpiredAsync(cutoff, options.ExpiryBatchSize, cancellationToken))
                .Where(order => attempted.Add(order.Id))
                .ToList();
            foreach (var order in batch)
            {
                switch (await TryCancelAsync(order, cutoff, cancellationToken))
                {
                    case Attempt.Cancelled:
                        cancelled++;
                        break;
                    case Attempt.Failed:
                        failed++;
                        break;
                    case Attempt.Skipped:
                        break;
                }
            }
        }
        while (batch.Count == options.ExpiryBatchSize);

        return new PaymentExpiryReport(cancelled, failed);
    }

    // One transaction for the cancel and the stock: if the stock cannot be given back, the order is not cancelled either.
    private async Task<Attempt> TryCancelAsync(Order order, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await repositories.BeginTransactionAsync(cancellationToken);
            if (!await repositories.Order.TryCancelExpiredAsync(order.Id, cutoff, cancellationToken))
            {
                // Paid, or cancelled by someone else, a moment ago: nothing to do, and not a failure.
                return Attempt.Skipped;
            }

            await OrderStockLines.GiveBackAsync(repositories, order, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Attempt.Cancelled;
        }
        catch (DbException) when (!cancellationToken.IsCancellationRequested)
        {
            return Attempt.Failed;
        }
    }
}
