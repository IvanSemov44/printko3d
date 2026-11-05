namespace Storefront.Domain.Catalog;

public sealed class ProductImage : Entity
{
    private ProductImage()
    {
    }

    internal ProductImage(
        Guid productId,
        string storageKey,
        string altText,
        int sortOrder,
        bool isPrimary,
        Guid? variantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentNullException.ThrowIfNull(altText);

        ProductId = productId;
        VariantId = variantId;
        StorageKey = storageKey;
        AltText = altText;
        SortOrder = sortOrder;
        IsPrimary = isPrimary;
    }

    public Guid ProductId { get; private set; }

    public Guid? VariantId { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string AltText { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsPrimary { get; private set; }
}
