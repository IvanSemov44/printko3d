namespace Storefront.Application.Payments;

/// <summary>How many unpaid orders one run cancelled, and how many it could not (a database failure; they are tried again next time).</summary>
public sealed record PaymentExpiryReport(int Cancelled, int Failed);

public interface IPaymentExpiryService
{
    /// <summary>
    /// Cancels the orders that are still new after their payment deadline plus the grace time, and gives their stock back.
    /// Each order has its own transaction, so a failure of one leaves the others alone and rolls back its own cancel with
    /// its own stock. An order paid at the same moment is left as paid: only one of the two wins.
    /// </summary>
    Task<PaymentExpiryReport> CancelExpiredAsync(CancellationToken cancellationToken);
}
