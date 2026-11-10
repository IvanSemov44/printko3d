using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Application.Catalog;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class CatalogServiceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task CreatedProduct_WithVariant_IsStoredAndPublished()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider.GetRequiredService<IServiceManager>();

        var categoryId = await services.Category.CreateAsync(new NewCategory("Figures", "figures"), default);
        var productId = await services.Product.CreateAsync(
            new NewProduct(categoryId, "Dragon", "dragon", "Short", "Long", FulfilmentType.InStock),
            default);
        await services.Product.AddVariantAsync(
            productId,
            new NewVariant("DRG-001", "Blue", 1500, 100, StockQuantity: 5),
            default);
        await services.Product.PublishAsync(productId, default);

        await using var read = database.CreateContext();
        var stored = await read.Products.Include(p => p.Variants).AsNoTracking().SingleAsync();
        Assert.Equal(ProductStatus.Published, stored.Status);
        Assert.Equal("DRG-001", Assert.Single(stored.Variants).Sku);
    }

    [Fact]
    public async Task PublishingAProductWithoutVariants_IsRejected_AndNothingChanges()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider.GetRequiredService<IServiceManager>();
        var categoryId = await services.Category.CreateAsync(new NewCategory("Figures", "figures"), default);
        var productId = await services.Product.CreateAsync(
            new NewProduct(categoryId, "Dragon", "dragon", "Short", "Long", FulfilmentType.InStock),
            default);

        var error = await Assert.ThrowsAsync<ConflictException>(() => services.Product.PublishAsync(productId, default));
        Assert.Equal("product.cannot_publish_without_active_variant", error.Code);

        await using var read = database.CreateContext();
        Assert.Equal(ProductStatus.Draft, (await read.Products.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task ProductWithATakenSlug_IsRejected_WithTheProductCode_NotTheCategoryOne()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider.GetRequiredService<IServiceManager>();
        var categoryId = await services.Category.CreateAsync(new NewCategory("Figures", "figures"), default);
        await services.Product.CreateAsync(
            new NewProduct(categoryId, "Dragon", "dragon", "Short", "Long", FulfilmentType.InStock),
            default);

        var error = await Assert.ThrowsAsync<ConflictException>(() => services.Product.CreateAsync(
            new NewProduct(categoryId, "Other dragon", "dragon", "Short", "Long", FulfilmentType.InStock),
            default));

        Assert.Equal("product.slug_taken", error.Code);
    }

    [Fact]
    public async Task VariantWithATakenSku_IsRejected_WithTheVariantCode_NotTheCategoryOne()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider.GetRequiredService<IServiceManager>();
        var categoryId = await services.Category.CreateAsync(new NewCategory("Figures", "figures"), default);
        var first = await services.Product.CreateAsync(
            new NewProduct(categoryId, "Dragon", "dragon", "Short", "Long", FulfilmentType.InStock),
            default);
        var second = await services.Product.CreateAsync(
            new NewProduct(categoryId, "Phoenix", "phoenix", "Short", "Long", FulfilmentType.InStock),
            default);
        await services.Product.AddVariantAsync(first, new NewVariant("DRG-001", "Blue", 1500, 100, StockQuantity: 5), default);

        var error = await Assert.ThrowsAsync<ConflictException>(() => services.Product.AddVariantAsync(
            second,
            new NewVariant("DRG-001", "Red", 1500, 100, StockQuantity: 5),
            default));

        Assert.Equal("variant.sku_taken", error.Code);
    }
}
