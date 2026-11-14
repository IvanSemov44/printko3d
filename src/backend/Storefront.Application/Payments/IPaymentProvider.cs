namespace Storefront.Application.Payments;

/// <summary>What the provider needs to open a payment: the order number to show, the sum and the last moment to pay.</summary>
public sealed record PaymentSessionRequest(int OrderNumber, long Amount, string Currency, DateTimeOffset ExpiresAt);

/// <summary>
/// An open payment at the provider. <see cref="RedirectUrl"/> is where the buyer is led to pay; the card is typed there,
/// never on our server (rule 9).
/// </summary>
public sealed record PaymentSession(string Reference, string RedirectUrl);

public enum PaymentOutcome
{
    Paid,
    Failed,
}

/// <summary>A message of the provider that passed the signature check. <see cref="EventId"/> is the provider's number for it.</summary>
public sealed record PaymentNotification(string EventId, string Reference, PaymentOutcome Outcome, long Amount);

/// <summary>The message as it arrived: the raw body and the signature header, before anything was read from it.</summary>
public sealed record PaymentMessage(string Body, string? Signature);

/// <summary>
/// Opens payments and proves that a message comes from the provider (decision 0035). The test provider works without an
/// outside service; a real one replaces it without a change in the payment service or the storefront.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Stable code of the provider, stored with every payment, for example <c>test</c>.</summary>
    string Code { get; }

    /// <summary>True for a provider that takes no real money. The storefront shows a notice for as long as this is true.</summary>
    bool IsTest { get; }

    /// <summary>
    /// Opens a payment that cannot be paid after <see cref="PaymentSessionRequest.ExpiresAt"/>. Nothing about the buyer
    /// is sent: only the number, the sum and the currency.
    /// </summary>
    Task<PaymentSession> CreateSessionAsync(PaymentSessionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the signature on the raw body first and only then reads it. Throws
    /// <see cref="PaymentMessageRejectedException"/> for a wrong, missing or stale signature and for a body that is not
    /// in the expected form; the reason is never told to the sender.
    /// </summary>
    Task<PaymentNotification> ReadNotificationAsync(PaymentMessage message, CancellationToken cancellationToken);
}
