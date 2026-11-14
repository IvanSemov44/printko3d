namespace Storefront.Application.Payments;

/// <summary>
/// The "provider side" of the test payment: it signs a message the way the real provider would, so the test page
/// travels the same road as a real payment. It exists only with the test provider (decision 0035).
/// </summary>
public interface IPaymentSimulator
{
    /// <summary>
    /// A signed message about the session with the given reference. Refuses a payment after the session expired, as a
    /// real provider does. <paramref name="eventId"/> is random when it is null; a test passes one to send a repeat.
    /// </summary>
    Task<PaymentMessage> CreateMessageAsync(
        string reference,
        PaymentOutcome outcome,
        long amount,
        string? eventId,
        CancellationToken cancellationToken);
}
