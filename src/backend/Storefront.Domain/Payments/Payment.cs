using Storefront.Domain.Orders;

namespace Storefront.Domain.Payments;

/// <summary>
/// One attempt to pay an order through a provider. The amount and the currency are copies of the order at the moment
/// the session was opened, so the provider and the order can be compared later. No card data is kept here (rule 9).
/// </summary>
public sealed class Payment : Entity
{
    public Guid OrderId { get; set; }

    /// <summary>Code of the provider, for example <c>test</c>.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The identifier the provider gave to the session.</summary>
    public string ProviderReference { get; set; } = string.Empty;

    /// <summary>Random identifier the storefront uses to ask about the payment. It does not reveal the order.</summary>
    public Guid PublicId { get; set; } = Guid.NewGuid();

    /// <summary>Price in the smallest currency unit.</summary>
    public long Amount { get; set; }

    /// <summary>ISO 4217 code copied from the order.</summary>
    public string Currency { get; set; } = string.Empty;

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>When the provider answered. Null while the payment is pending.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>PostgreSQL row version (xmin): a message and the expiry of the order may meet at the same moment.</summary>
    public uint Version { get; set; }

    public Order? Order { get; set; }
}
