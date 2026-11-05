namespace Storefront.Domain.Catalog;

public sealed class ProductVariant : Entity
{
    private ProductVariant()
    {
    }

    internal ProductVariant(
        Guid productId,
        string sku,
        string name,
        long price,
        int weightGrams,
        long? compareAtPrice,
        int? stockQuantity,
        IReadOnlyDictionary<string, string>? attributes,
        bool isActive,
        int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        ArgumentOutOfRangeException.ThrowIfNegative(weightGrams);
        if (compareAtPrice is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(compareAtPrice.Value);
        }

        if (stockQuantity is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(stockQuantity.Value);
        }

        ProductId = productId;
        Sku = sku;
        Name = name;
        Price = price;
        CompareAtPrice = compareAtPrice;
        StockQuantity = stockQuantity;
        WeightGrams = weightGrams;
        Attributes = attributes is null ? [] : new Dictionary<string, string>(attributes);
        IsActive = isActive;
        SortOrder = sortOrder;
    }

    public Guid ProductId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public long Price { get; private set; }

    public long? CompareAtPrice { get; private set; }

    public int? StockQuantity { get; private set; }

    public int WeightGrams { get; private set; }

    public Dictionary<string, string> Attributes { get; private set; } = [];

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }
}
