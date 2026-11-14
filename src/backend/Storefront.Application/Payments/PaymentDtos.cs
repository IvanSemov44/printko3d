namespace Storefront.Application.Payments;

/// <summary>
/// An open payment of an order. <paramref name="PaymentId"/> is the random identifier the storefront uses to ask about
/// the payment; it does not reveal the order. <paramref name="RedirectUrl"/> is where the buyer pays.
/// </summary>
public sealed record PaymentSessionDto(Guid PaymentId, string RedirectUrl, DateTimeOffset ExpiresAt);

/// <summary>
/// The state of a payment and nothing else: no name, email, address or order. <paramref name="Status"/> is
/// <c>Pending</c>, <c>Succeeded</c>, <c>Failed</c>, <c>PaidWithoutOrder</c> or <c>Expired</c> (the order was cancelled
/// before the provider answered).
/// </summary>
public sealed record PaymentStatusDto(string Status);

/// <summary>What a message of the provider led to. The API writes it to the journal, without anything about the buyer.</summary>
public enum PaymentNotificationResult
{
    /// <summary>The payment was recorded and the order is paid, or the payment failed.</summary>
    Applied,

    /// <summary>The payment came after the order was cancelled, the stock was taken again and the order is paid.</summary>
    Revived,

    /// <summary>The payment came after the order was cancelled and the stock could not be taken again. The money is returned by hand.</summary>
    PaidWithoutOrder,

    /// <summary>The same message was handled before. Nothing changed.</summary>
    Repeated,

    /// <summary>The message is about a session we do not know. Nothing changed.</summary>
    UnknownPayment,

    /// <summary>The sum in the message is not the sum of the payment. The order was not paid.</summary>
    AmountMismatch,

    /// <summary>The payment was not waiting any more, so the message changed nothing.</summary>
    Ignored,
}
