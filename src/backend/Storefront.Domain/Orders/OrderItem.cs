using Storefront.Domain.Catalog;

namespace Storefront.Domain.Orders;

/// <summary>A line of an order. Name, SKU and price are copies from the moment of purchase, not a reference to read later.</summary>
public sealed class OrderItem : Entity
{
    public Guid OrderId { get; set; }

    /// <summary>The variant that was sold. A variant with an order cannot be deleted.</summary>
    public Guid VariantId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public long UnitPrice { get; set; }

    public int Quantity { get; set; }

    public long LineTotal { get; set; }

    public FulfilmentType FulfilmentType { get; set; }

    public int? LeadTimeDays { get; set; }
}
