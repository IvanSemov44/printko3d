namespace Storefront.Domain.Orders;

/// <summary>
/// An order placed by a guest. The contact, the address and the lines are copies taken at the moment of purchase,
/// so a later change of a product, a variant or the store settings never changes an old order.
/// </summary>
public sealed class Order : Entity
{
    public const int MinNumber = 10_000_000;

    public const int MaxNumber = 99_999_999;

    /// <summary>Random eight-digit number shown to the buyer. It is not a secret and is not an access key.</summary>
    public int Number { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.New;

    /// <summary>ISO 4217 code copied from the store settings.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Sum of the lines in the smallest currency unit.</summary>
    public long TotalAmount { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string ShippingStreet { get; set; } = string.Empty;

    public string ShippingCity { get; set; } = string.Empty;

    public string ShippingPostalCode { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 code.</summary>
    public string ShippingCountry { get; set; } = string.Empty;

    /// <summary>The version of the terms and conditions the buyer agreed to.</summary>
    public string TermsVersion { get; set; } = string.Empty;

    public DateTimeOffset TermsAcceptedAt { get; set; }

    /// <summary>When the reserved stock was given back (refusal or expiry). Null while the order still holds it. Set once, so a second refusal cannot give it back twice.</summary>
    public DateTimeOffset? StockReleasedAt { get; set; }

    /// <summary>PostgreSQL row version (xmin). Payment and the admin both change the status, so two writers must be detected.</summary>
    public uint Version { get; set; }

    public List<OrderItem> Items { get; set; } = [];
}
