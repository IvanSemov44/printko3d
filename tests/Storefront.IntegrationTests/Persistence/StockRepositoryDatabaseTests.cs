using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Application.Contracts;
using Storefront.Domain.Catalog;
using Storefront.Domain.Orders;

namespace Storefront.IntegrationTests.Persistence;

/// <summary>
/// The conditional stock statements and the transaction, against a real PostgreSQL. Every call gets its own scope,
/// like a separate request, so two calls at once really are two connections.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class StockRepositoryDatabaseTests(PostgresFixture postgres)
{
    private static async Task<Guid> SeedVariantAsync(TestDatabase database, int? stock)
    {
        await using var context = database.CreateContext();
        var category = CatalogData.Category();
        var product = CatalogData.Product(
            category.Id,
            fulfilmentType: stock is null ? FulfilmentType.MadeToOrder : FulfilmentType.InStock,
            leadTimeDays: stock is null ? 7 : null);
        var variant = CatalogData.Variant(product);
        variant.StockQuantity = stock;
        context.Add(category);
        context.Add(product);
        await context.SaveChangesAsync();
        return variant.Id;
    }

    private static async Task<int?> StockOfAsync(TestDatabase database, Guid variantId)
    {
        await using var context = database.CreateContext();
        return await context.Set<ProductVariant>().AsNoTracking()
            .Where(v => v.Id == variantId)
            .Select(v => v.StockQuantity)
            .SingleAsync();
    }

    private static async Task<T> InScopeAsync<T>(ApiFactory factory, Func<IRepositoryManager, Task<T>> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<IRepositoryManager>());
    }

    [Theory]
    [InlineData(5, 2, true, 3)]
    [InlineData(5, 5, true, 0)]
    [InlineData(5, 6, false, 5)]
    [InlineData(0, 1, false, 0)]
    public async Task TakeStock_ChangesTheStockOnlyWhenThereIsEnough(int stock, int quantity, bool taken, int left)
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        var variantId = await SeedVariantAsync(database, stock);

        var result = await InScopeAsync(
            factory, repositories => repositories.Product.TryTakeStockAsync(variantId, quantity, default));

        Assert.Equal(taken, result);
        Assert.Equal(left, await StockOfAsync(database, variantId));
    }

    [Fact]
    public async Task TakeStock_LeavesAMadeToOrderVariantWithoutStockFigure()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        var variantId = await SeedVariantAsync(database, stock: null);

        var result = await InScopeAsync(
            factory, repositories => repositories.Product.TryTakeStockAsync(variantId, 1, default));

        Assert.False(result);
        Assert.Null(await StockOfAsync(database, variantId));
    }

    [Fact]
    public async Task TakeStock_TwoBuyersAtOnceForTheLastPiece_OnlyOneGetsIt()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        var variantId = await SeedVariantAsync(database, stock: 1);
        var start = new TaskCompletionSource();

        async Task<bool> BuyAsync()
        {
            await start.Task;
            return await InScopeAsync(
                factory, repositories => repositories.Product.TryTakeStockAsync(variantId, 1, default));
        }

        var buyers = Enumerable.Range(0, 8).Select(_ => Task.Run(BuyAsync)).ToList();
        start.SetResult();
        var results = await Task.WhenAll(buyers);

        Assert.Single(results, taken => taken);
        Assert.Equal(0, await StockOfAsync(database, variantId));
    }

    [Fact]
    public async Task ReturnStock_AddsTheQuantityBack_AndLeavesAMadeToOrderVariantAlone()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        var inStock = await SeedVariantAsync(database, stock: 3);

        await InScopeAsync(factory, async repositories =>
        {
            await repositories.Product.ReturnStockAsync(inStock, 2, default);
            return true;
        });
        Assert.Equal(5, await StockOfAsync(database, inStock));

        var otherDatabase = await postgres.CreateDatabaseAsync();
        await using var otherFactory = new ApiFactory(otherDatabase.ConnectionString);
        var madeToOrder = await SeedVariantAsync(otherDatabase, stock: null);
        await InScopeAsync(otherFactory, async repositories =>
        {
            await repositories.Product.ReturnStockAsync(madeToOrder, 2, default);
            return true;
        });
        Assert.Null(await StockOfAsync(otherDatabase, madeToOrder));
    }

    [Fact]
    public async Task Transaction_DisposedWithoutCommit_UndoesTheStockTaken()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        var variantId = await SeedVariantAsync(database, stock: 5);

        await InScopeAsync(factory, async repositories =>
        {
            await using var transaction = await repositories.BeginTransactionAsync(default);
            Assert.True(await repositories.Product.TryTakeStockAsync(variantId, 2, default));
            return true;
        });

        Assert.Equal(5, await StockOfAsync(database, variantId));
    }

    [Fact]
    public async Task Transaction_Committed_KeepsTheStockTaken()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        var variantId = await SeedVariantAsync(database, stock: 5);

        await InScopeAsync(factory, async repositories =>
        {
            await using var transaction = await repositories.BeginTransactionAsync(default);
            Assert.True(await repositories.Product.TryTakeStockAsync(variantId, 2, default));
            await transaction.CommitAsync(default);
            return true;
        });

        Assert.Equal(3, await StockOfAsync(database, variantId));
    }

    [Fact]
    public async Task MarkStockReleased_WorksOncePerOrder()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        var orderId = await SeedOrderAsync(database);

        var first = await InScopeAsync(
            factory, repositories => repositories.Order.TryMarkStockReleasedAsync(orderId, default));
        var second = await InScopeAsync(
            factory, repositories => repositories.Order.TryMarkStockReleasedAsync(orderId, default));

        Assert.True(first);
        Assert.False(second);
        await using var context = database.CreateContext();
        Assert.NotNull(await context.Orders.AsNoTracking()
            .Where(o => o.Id == orderId).Select(o => o.StockReleasedAt).SingleAsync());
    }

    private static async Task<Guid> SeedOrderAsync(TestDatabase database)
    {
        await using var context = database.CreateContext();
        var order = new Order
        {
            Number = 12345678,
            Currency = "BGN",
            TotalAmount = 3000,
            CustomerName = "Test Buyer",
            Email = "buyer@example.com",
            Phone = "+359000000",
            ShippingStreet = "1 Test Street",
            ShippingCity = "Sofia",
            ShippingPostalCode = "1000",
            ShippingCountry = "BG",
            TermsVersion = "draft-1",
            TermsAcceptedAt = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero),
        };
        context.Add(order);
        await context.SaveChangesAsync();
        return order.Id;
    }
}
