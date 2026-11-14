using Storefront.Domain.Payments;

namespace Storefront.Application.Contracts;

public interface IPaymentRepository
{
    void Create(Payment payment);

    /// <summary>Marks a handled message of the provider as new for the next save. The unique index refuses a repeat.</summary>
    void CreateEvent(PaymentEvent paymentEvent);

    /// <summary>The payment with its order, for the status page. Not tracked. Null when there is none.</summary>
    Task<Payment?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken);

    /// <summary>The payment of a provider's session, tracked so that the caller can change its status. Null when there is none.</summary>
    Task<Payment?> GetByReferenceAsync(string provider, string reference, CancellationToken cancellationToken);

    /// <summary>The payment of the order that still waits for the provider, or null. Not tracked.</summary>
    Task<Payment?> GetPendingForOrderAsync(Guid orderId, CancellationToken cancellationToken);
}
