namespace Storefront.Domain.Catalog;

public sealed class ProductVariant : Entity
{
    public Guid ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public long Price { get; set; }

    public long? CompareAtPrice { get; set; }

    public int? StockQuantity { get; set; }

    public int WeightGrams { get; set; }

    public Dictionary<string, string> Attributes { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
