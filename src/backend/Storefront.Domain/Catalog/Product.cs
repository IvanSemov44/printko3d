namespace Storefront.Domain.Catalog;

public sealed class Product : Entity
{
    public const int ShortDescriptionMaxLength = 200;

    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductImage> _images = [];

    private Product()
    {
    }

    public Product(
        Guid categoryId,
        string name,
        string slug,
        string shortDescription,
        string description,
        FulfilmentType fulfilmentType,
        int? leadTimeDays = null,
        string? seoTitle = null,
        string? seoDescription = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(shortDescription);
        ArgumentNullException.ThrowIfNull(description);
        if (shortDescription.Length > ShortDescriptionMaxLength)
        {
            throw new ArgumentException(
                $"The short description is at most {ShortDescriptionMaxLength} characters.",
                nameof(shortDescription));
        }

        if (fulfilmentType == FulfilmentType.MadeToOrder && leadTimeDays is null)
        {
            throw new InvalidOperationException("Lead time is required for made-to-order products.");
        }

        if (leadTimeDays is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(leadTimeDays.Value);
        }

        CategoryId = categoryId;
        Name = name;
        Slug = slug;
        ShortDescription = shortDescription;
        Description = description;
        FulfilmentType = fulfilmentType;
        LeadTimeDays = leadTimeDays;
        Status = ProductStatus.Draft;
        SeoTitle = seoTitle;
        SeoDescription = seoDescription;
    }

    public Guid CategoryId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string ShortDescription { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public FulfilmentType FulfilmentType { get; private set; }

    public int? LeadTimeDays { get; private set; }

    public ProductStatus Status { get; private set; }

    public string? SeoTitle { get; private set; }

    public string? SeoDescription { get; private set; }

    public IReadOnlyList<ProductVariant> Variants => _variants;

    public IReadOnlyList<ProductImage> Images => _images;

    public ProductVariant AddVariant(
        string sku,
        string name,
        long price,
        int weightGrams,
        long? compareAtPrice = null,
        int? stockQuantity = null,
        IReadOnlyDictionary<string, string>? attributes = null,
        bool isActive = true,
        int sortOrder = 0)
    {
        var variant = new ProductVariant(
            Id, sku, name, price, weightGrams, compareAtPrice, stockQuantity, attributes, isActive, sortOrder);
        _variants.Add(variant);
        return variant;
    }

    public ProductImage AddImage(
        string storageKey,
        string altText,
        int sortOrder = 0,
        bool isPrimary = false,
        Guid? variantId = null)
    {
        var image = new ProductImage(Id, storageKey, altText, sortOrder, isPrimary, variantId);
        _images.Add(image);
        return image;
    }

    public void Publish()
    {
        if (_variants.Count == 0)
        {
            throw new InvalidOperationException("A product needs at least one variant to be published.");
        }

        Status = ProductStatus.Published;
    }

    public void Archive() => Status = ProductStatus.Archived;
}
