namespace Storefront.Domain.Catalog;

public sealed class ProductImage : Entity
{
    public Guid ProductId { get; set; }

    public Guid? VariantId { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string AltText { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsPrimary { get; set; }
}
