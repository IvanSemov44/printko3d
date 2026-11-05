using Storefront.Domain.Catalog;

namespace Storefront.IntegrationTests.Persistence;

internal static class CatalogData
{
    public static Category Category(string slug = "figures", Category? parent = null) =>
        new("Figures", slug, parent);

    public static Product Product(
        Guid categoryId,
        string slug = "dragon",
        FulfilmentType fulfilmentType = FulfilmentType.InStock,
        int? leadTimeDays = null) =>
        new(categoryId, "Dragon", slug, "Short", "Long", fulfilmentType, leadTimeDays);

    public static ProductVariant Variant(Product product, string sku = "DRG-001") =>
        product.AddVariant(sku, "Blue", price: 1500, weightGrams: 100, stockQuantity: 5);

    public static StoreSettings Settings() =>
        new("Printko 3D", "BGN", "bg", "shop@example.com", "+359000000", "#112233");
}

internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public void Advance(TimeSpan by) => _now += by;

    public override DateTimeOffset GetUtcNow() => _now;
}
