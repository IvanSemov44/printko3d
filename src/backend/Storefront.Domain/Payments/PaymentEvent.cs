namespace Storefront.Domain.Payments;

/// <summary>
/// A message of the provider that was already handled. The unique pair of provider and event number is what makes a
/// repeated message harmless: the second insert fails and nothing else changes.
/// </summary>
public sealed class PaymentEvent : Entity
{
    public Guid PaymentId { get; set; }

    public string Provider { get; set; } = string.Empty;

    /// <summary>The number the provider gave to the message.</summary>
    public string EventId { get; set; } = string.Empty;
}
