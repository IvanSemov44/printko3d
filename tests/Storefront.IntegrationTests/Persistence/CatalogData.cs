using Storefront.Domain.Catalog;

namespace Storefront.IntegrationTests.Persistence;

internal static class CatalogData
{
    public static Category Category(string slug = "figures", Category? parent = null) =>
        new() { Name = "Figures", Slug = slug, ParentId = parent?.Id };

    public static Product Product(
        Guid categoryId,
        string slug = "dragon",
        FulfilmentType fulfilmentType = FulfilmentType.InStock,
        int? leadTimeDays = null) =>
        new()
        {
            CategoryId = categoryId,
            Name = "Dragon",
            Slug = slug,
            ShortDescription = "Short",
            Description = "Long",
            FulfilmentType = fulfilmentType,
            LeadTimeDays = leadTimeDays,
        };

    public static ProductVariant Variant(
        Product product,
        string sku = "DRG-001",
        Dictionary<string, string>? attributes = null)
    {
        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Sku = sku,
            Name = "Blue",
            Price = 1500,
            WeightGrams = 100,
            StockQuantity = 5,
            Attributes = attributes ?? [],
        };
        product.Variants.Add(variant);
        return variant;
    }

    public static ProductImage Image(
        Product product,
        string storageKey,
        bool isPrimary = false,
        Guid? variantId = null)
    {
        var image = new ProductImage
        {
            ProductId = product.Id,
            VariantId = variantId,
            StorageKey = storageKey,
            AltText = "alt",
            IsPrimary = isPrimary,
        };
        product.Images.Add(image);
        return image;
    }

    public static StoreSettings Settings() => new()
    {
        StoreName = "Printko 3D",
        DefaultCurrency = "BGN",
        DefaultLocale = "bg",
        ContactEmail = "shop@example.com",
        ContactPhone = "+359000000",
        PrimaryColor = "#112233",
    };
}

internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public void Advance(TimeSpan by) => _now += by;

    public override DateTimeOffset GetUtcNow() => _now;
}
