namespace Storefront.Application.Payments;

public interface IPaymentService
{
    /// <summary>
    /// Makes sure the order has a payment waiting at the provider and returns it. The sum and the currency come from the
    /// order, never from the caller. A second call while the payment waits returns the same session.
    /// Errors: 404 order.not_found, 404 payment.disabled (payment is turned off), 409 payment.order_not_payable (the order
    /// is paid, cancelled, or past its deadline).
    /// </summary>
    Task<PaymentSessionDto> StartAsync(int orderNumber, CancellationToken cancellationToken);

    /// <summary>Whether the payment is a test one, for the notice of the storefront.</summary>
    Task<PaymentModeDto> GetModeAsync(CancellationToken cancellationToken);

    /// <summary>Only the state of the payment, by its random identifier. Errors: 404 payment.not_found.</summary>
    Task<PaymentStatusDto> GetStatusAsync(Guid paymentId, CancellationToken cancellationToken);

    /// <summary>
    /// Handles a message of the provider: the signature is checked on the raw body before anything is read, a repeated
    /// message changes nothing, and the sum must match. Throws <see cref="PaymentMessageRejectedException"/> when the
    /// message does not come from the provider. Everything it changes is one transaction.
    /// </summary>
    Task<PaymentNotificationReport> HandleNotificationAsync(PaymentMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// The test page: the provider side signs a message about the session and it goes the same road as a real one.
    /// Errors: 404 payment.not_found, 404 payment.disabled, 422 payment.session_expired (a payment after the session ended).
    /// </summary>
    Task<PaymentNotificationReport> CompleteTestPaymentAsync(
        string reference,
        PaymentOutcome outcome,
        CancellationToken cancellationToken);
}
