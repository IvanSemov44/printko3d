namespace Storefront.Application.Payments;

/// <summary>Settings of paying an order (decision 0035). Bound from the <c>Payment</c> section and checked at startup.</summary>
public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    /// <summary>
    /// False turns payment off: the payment addresses answer 404, as the login does when it is off (decision 0013).
    /// It is the only way to start outside Development and Demo while the test provider is the only provider.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>How long a buyer has to pay after confirming the order. Also when the provider's session ends.</summary>
    public int WindowMinutes { get; init; } = 60;

    /// <summary>
    /// How long the cancellation of an unpaid order waits after the deadline, so that a payment started before the
    /// deadline can still arrive.
    /// </summary>
    public int GraceMinutes { get; init; } = 5;

    public static bool IsValid(PaymentOptions options) =>
        options.WindowMinutes is > 0 and <= 7 * 24 * 60 && options.GraceMinutes is >= 0 and <= 24 * 60;
}
