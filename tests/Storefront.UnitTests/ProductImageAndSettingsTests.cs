using Storefront.Application.Catalog;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

public sealed class ProductImageAndSettingsTests
{
    private readonly FakeRepositoryManager _repositories = new();
    private readonly ServiceManager _services;

    public ProductImageAndSettingsTests() => _services = new ServiceManager(_repositories, new FakeImageStorage());

    private async Task<Guid> CreateProductAsync(string slug = "dragon", string sku = "SKU-1")
    {
        var categoryId = _repositories.Categories.FirstOrDefault()?.Id
            ?? await _services.Category.CreateAsync(new NewCategory("Figures", "figures"), default);
        var productId = await _services.Product.CreateAsync(
            new NewProduct(categoryId, "Dragon", slug, "Short", "Long", FulfilmentType.InStock),
            default);
        await _services.Product.AddVariantAsync(productId, new NewVariant(sku, "Blue", 100, 10, StockQuantity: 5), default);
        return productId;
    }

    private static NewStoreSettings Settings(
        string currency = "EUR",
        string color = "#112233",
        string email = "demo@example.com") =>
        new("Shop", currency, "bg", email, "000", color, IsDemo: true);

    [Fact]
    public async Task FirstImage_BecomesPrimary_EvenWhenNotRequested()
    {
        var productId = await CreateProductAsync();

        await _services.Product.AddImageAsync(productId, new NewImage("k/1.jpg", "Alt"), default);

        Assert.True(Assert.Single(_repositories.Products[0].Images).IsPrimary);
    }

    [Fact]
    public async Task NewPrimaryImage_ReplacesTheOldOne()
    {
        var productId = await CreateProductAsync();
        await _services.Product.AddImageAsync(productId, new NewImage("k/1.jpg", "Alt"), default);

        await _services.Product.AddImageAsync(productId, new NewImage("k/2.jpg", "Alt", IsPrimary: true), default);

        var primary = Assert.Single(_repositories.Products[0].Images, i => i.IsPrimary);
        Assert.Equal("k/2.jpg", primary.StorageKey);
    }

    [Fact]
    public async Task NonPrimaryImage_KeepsTheExistingPrimary()
    {
        var productId = await CreateProductAsync();
        await _services.Product.AddImageAsync(productId, new NewImage("k/1.jpg", "Alt"), default);

        await _services.Product.AddImageAsync(productId, new NewImage("k/2.jpg", "Alt"), default);

        var primary = Assert.Single(_repositories.Products[0].Images, i => i.IsPrimary);
        Assert.Equal("k/1.jpg", primary.StorageKey);
    }

    [Fact]
    public async Task Image_BoundToAVariantOfAnotherProduct_IsRejected()
    {
        var productId = await CreateProductAsync("dragon", "SKU-1");
        var otherId = await CreateProductAsync("owl", "SKU-2");
        var otherVariantId = _repositories.Products.Single(p => p.Id == otherId).Variants.Single().Id;

        await Assert.ThrowsAsync<ValidationFailedException>(() => _services.Product.AddImageAsync(
            productId, new NewImage("k/1.jpg", "Alt", VariantId: otherVariantId), default));
        Assert.Empty(_repositories.Products.Single(p => p.Id == productId).Images);
    }

    [Fact]
    public async Task Image_BoundToAVariantOfTheSameProduct_IsAccepted()
    {
        var productId = await CreateProductAsync();
        var variantId = _repositories.Products[0].Variants.Single().Id;

        await _services.Product.AddImageAsync(productId, new NewImage("k/1.jpg", "Alt", VariantId: variantId), default);

        Assert.Equal(variantId, Assert.Single(_repositories.Products[0].Images).VariantId);
    }

    [Theory]
    [InlineData("", "Alt")]
    [InlineData("k/1.jpg", " ")]
    public async Task Image_WithoutKeyOrAltText_IsRejected(string key, string alt)
    {
        var productId = await CreateProductAsync();

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Product.AddImageAsync(productId, new NewImage(key, alt), default));
    }

    [Fact]
    public async Task Archiving_SetsTheStatus_AndRejectsUnknownProducts()
    {
        var productId = await CreateProductAsync();

        await _services.Product.ArchiveAsync(productId, default);

        Assert.Equal(ProductStatus.Archived, _repositories.Products[0].Status);
        await Assert.ThrowsAsync<NotFoundException>(
            () => _services.Product.ArchiveAsync(Guid.CreateVersion7(), default));
    }

    [Fact]
    public async Task Settings_AreCreatedOnce_AndNotOverwritten()
    {
        Assert.True(await _services.StoreSettings.EnsureAsync(Settings(), default));

        var second = await _services.StoreSettings.EnsureAsync(Settings() with { StoreName = "Other" }, default);

        Assert.False(second);
        var stored = Assert.Single(_repositories.Settings);
        Assert.Equal("Shop", stored.StoreName);
        Assert.True(stored.IsDemo);
    }

    [Theory]
    [InlineData("eur", "#112233", "a@b.c")]
    [InlineData("EURO", "#112233", "a@b.c")]
    [InlineData("EUR", "blue", "a@b.c")]
    [InlineData("EUR", "#112233", "not-an-email")]
    public async Task Settings_WithInvalidValues_AreRejected(string currency, string color, string email)
    {
        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.StoreSettings.EnsureAsync(Settings(currency, color, email), default));
        Assert.Empty(_repositories.Settings);
    }
}
