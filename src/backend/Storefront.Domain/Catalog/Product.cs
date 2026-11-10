namespace Storefront.Domain.Catalog;

public sealed class Product : Entity
{
    public const int ShortDescriptionMaxLength = 200;

    public Guid CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string ShortDescription { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public FulfilmentType FulfilmentType { get; set; }

    public int? LeadTimeDays { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Draft;

    public string? SeoTitle { get; set; }

    public string? SeoDescription { get; set; }

    /// <summary>PostgreSQL row version (xmin). Changes on every write to the product row; used to detect two people editing at once.</summary>
    public uint Version { get; set; }

    public ICollection<ProductVariant> Variants { get; set; } = [];

    public ICollection<ProductImage> Images { get; set; } = [];
}
