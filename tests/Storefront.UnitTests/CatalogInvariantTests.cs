using Storefront.Application.Catalog;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

public sealed class CatalogInvariantTests
{
    private readonly FakeRepositoryManager _repositories = new();
    private readonly ServiceManager _services;

    public CatalogInvariantTests() => _services = new ServiceManager(_repositories, new FakeImageStorage());

    private static NewVariant Variant(
        long price = 100,
        int? stockQuantity = 5,
        string sku = "SKU-1") =>
        new(sku, "Blue", price, WeightGrams: 10, StockQuantity: stockQuantity);

    private async Task<Guid> CreateCategoryAsync()
    {
        var id = await _services.Category.CreateAsync(new NewCategory("Figures", "figures"), default);
        return id;
    }

    private async Task<Guid> CreateProductAsync(
        FulfilmentType type = FulfilmentType.InStock,
        int? leadTimeDays = null,
        string shortDescription = "Short")
    {
        var categoryId = await CreateCategoryAsync();
        return await _services.Product.CreateAsync(
            new NewProduct(categoryId, "Dragon", "dragon", shortDescription, "Long", type, leadTimeDays),
            default);
    }

    [Fact]
    public async Task MadeToOrderProduct_WithoutLeadTime_IsRejected()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(
            () => CreateProductAsync(FulfilmentType.MadeToOrder));
        Assert.Empty(_repositories.Products);
    }

    [Fact]
    public async Task MadeToOrderProduct_WithLeadTime_IsAccepted()
    {
        await CreateProductAsync(FulfilmentType.MadeToOrder, leadTimeDays: 7);

        Assert.Equal(7, Assert.Single(_repositories.Products).LeadTimeDays);
    }

    [Fact]
    public async Task InStockProduct_WithoutLeadTime_IsAccepted()
    {
        await CreateProductAsync();

        Assert.Null(Assert.Single(_repositories.Products).LeadTimeDays);
    }

    [Fact]
    public async Task ShortDescription_LongerThan200Characters_IsRejected()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(
            () => CreateProductAsync(shortDescription: new string('a', 201)));
    }

    [Fact]
    public async Task Product_InUnknownCategory_IsRejected()
    {
        var product = new NewProduct(
            Guid.CreateVersion7(), "Dragon", "dragon", "Short", "Long", FulfilmentType.InStock);

        await Assert.ThrowsAsync<NotFoundException>(() => _services.Product.CreateAsync(product, default));
    }

    [Fact]
    public async Task Variant_WithNegativePrice_IsRejected()
    {
        var productId = await CreateProductAsync();

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Product.AddVariantAsync(productId, Variant(price: -1), default));
        Assert.Empty(_repositories.Products[0].Variants);
    }

    [Fact]
    public async Task Variant_WithNegativeStock_IsRejected()
    {
        var productId = await CreateProductAsync();

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Product.AddVariantAsync(productId, Variant(stockQuantity: -1), default));
    }

    [Fact]
    public async Task Variant_WithZeroPriceAndStock_IsAccepted()
    {
        var productId = await CreateProductAsync();

        await _services.Product.AddVariantAsync(productId, Variant(price: 0, stockQuantity: 0), default);

        var variant = Assert.Single(_repositories.Products[0].Variants);
        Assert.Equal(0, variant.Price);
        Assert.Equal(0, variant.StockQuantity);
    }

    [Fact]
    public async Task Category_UnderASubcategory_IsRejected()
    {
        var rootId = await CreateCategoryAsync();
        var childId = await _services.Category.CreateAsync(new NewCategory("Child", "child", rootId), default);

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Category.CreateAsync(new NewCategory("Grandchild", "grandchild", childId), default));
    }

    [Fact]
    public async Task Category_UnderARootCategory_IsAccepted()
    {
        var rootId = await CreateCategoryAsync();

        var childId = await _services.Category.CreateAsync(new NewCategory("Child", "child", rootId), default);

        Assert.Equal(rootId, _repositories.Categories.Single(c => c.Id == childId).ParentId);
    }

    [Fact]
    public async Task Product_WithoutVariants_CannotBePublished()
    {
        var productId = await CreateProductAsync();

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => _services.Product.PublishAsync(productId, default));
        Assert.Equal("product.cannot_publish_without_active_variant", error.Code);
        Assert.Equal(ProductStatus.Draft, _repositories.Products[0].Status);
    }

    [Fact]
    public async Task Product_WithAVariant_CanBePublished()
    {
        var productId = await CreateProductAsync();
        await _services.Product.AddVariantAsync(productId, Variant(), default);

        await _services.Product.PublishAsync(productId, default);

        Assert.Equal(ProductStatus.Published, _repositories.Products[0].Status);
    }

    [Fact]
    public async Task Publishing_UnknownProduct_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _services.Product.PublishAsync(Guid.CreateVersion7(), default));
    }
}
