using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Application.Catalog;
using Storefront.Application.Exceptions;
using Storefront.Application.Orders;
using Storefront.Domain.Catalog;

namespace Storefront.IntegrationTests.Persistence;

/// <summary>
/// What an order does to the stock (task 24), against a real PostgreSQL. Every call gets its own scope, like a
/// separate request.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderStockDatabaseTests(PostgresFixture postgres)
{
    // The ids are fixed so that the first variant always sorts before the second: the service takes stock in id order.
    private static readonly Guid FirstVariantId = new("00000000-0000-0000-0000-0000000000a1");

    private static readonly Guid SecondVariantId = new("00000000-0000-0000-0000-0000000000a2");

    private sealed record Setup(TestDatabase Database, WebApplicationFactory<Program> Factory) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }

    private static PlaceOrderInput Input(long expectedTotal) =>
        new("Test Buyer", "buyer@example.com", "+359 88 000 0000", "1 Test Street", "Sofia", "1000", true, expectedTotal);

    /// <summary>
    /// One in-stock variant (price 1500) and one made to order (price 1500, no stock figure). The second is only there
    /// when <paramref name="secondStock"/> is given: a stock figure makes it an in-stock one as well.
    /// </summary>
    private async Task<Setup> CreateAsync(int firstStock, bool madeToOrderSecond = false, int? secondStock = null)
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var category = CatalogData.Category();
        context.Add(category);
        context.Add(CatalogData.Settings());

        var first = CatalogData.Product(category.Id, "dragon");
        first.Status = ProductStatus.Published;
        var firstVariant = CatalogData.Variant(first, "DRG-001");
        firstVariant.Id = FirstVariantId;
        firstVariant.StockQuantity = firstStock;
        context.Add(first);

        if (madeToOrderSecond || secondStock is not null)
        {
            var second = CatalogData.Product(
                category.Id,
                "knight",
                madeToOrderSecond ? FulfilmentType.MadeToOrder : FulfilmentType.InStock,
                madeToOrderSecond ? 7 : null);
            second.Status = ProductStatus.Published;
            var secondVariant = CatalogData.Variant(second, "KNT-001");
            secondVariant.Id = SecondVariantId;
            secondVariant.StockQuantity = madeToOrderSecond ? null : secondStock;
            context.Add(second);
        }

        await context.SaveChangesAsync();
        return new Setup(database, new ApiFactory(database.ConnectionString));
    }

    private static async Task<T> InScopeAsync<T>(Setup setup, Func<IServiceManager, Task<T>> action)
    {
        await using var scope = setup.Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IServiceManager>());
    }

    private static async Task<string?> CartWithAsync(Setup setup, params (Guid VariantId, int Quantity)[] lines)
    {
        string? token = null;
        foreach (var (variantId, quantity) in lines)
        {
            var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(token, variantId, quantity, default));
            token = change.Token ?? token;
        }

        return token;
    }

    private static async Task<OrderDto> PlaceAsync(Setup setup, string? token, long expectedTotal) =>
        await InScopeAsync(setup, s => s.Orders.PlaceAsync(token, Input(expectedTotal), default));

    private static async Task<int?> StockOfAsync(Setup setup, Guid variantId)
    {
        await using var context = setup.Database.CreateContext();
        return await context.ProductVariants.AsNoTracking()
            .Where(v => v.Id == variantId).Select(v => v.StockQuantity).SingleAsync();
    }

    private static async Task<int> OrderCountAsync(Setup setup)
    {
        await using var context = setup.Database.CreateContext();
        return await context.Orders.CountAsync();
    }

    private static async Task<Guid> OnlyOrderIdAsync(Setup setup)
    {
        await using var context = setup.Database.CreateContext();
        return await context.Orders.AsNoTracking().Select(o => o.Id).SingleAsync();
    }

    [Fact]
    public async Task Place_TakesTheStockOfInStockLines_AndLeavesMadeToOrderLinesAlone()
    {
        await using var setup = await CreateAsync(firstStock: 5, madeToOrderSecond: true);
        var token = await CartWithAsync(setup, (FirstVariantId, 2), (SecondVariantId, 1));

        await PlaceAsync(setup, token, expectedTotal: 4500);

        Assert.Equal(3, await StockOfAsync(setup, FirstVariantId));
        Assert.Null(await StockOfAsync(setup, SecondVariantId));
    }

    [Fact]
    public async Task Place_WhenTheLastPieceGoesBetweenTheCheckAndTheSave_RefusesAndGivesEverythingBack()
    {
        await using var setup = await CreateAsync(firstStock: 5, secondStock: 5);
        var token = await CartWithAsync(setup, (FirstVariantId, 2), (SecondVariantId, 1));

        // Another buyer holds the second variant and takes its last pieces; the order waits for that row.
        await using var rival = setup.Database.CreateContext();
        await using var rivalTransaction = await rival.Database.BeginTransactionAsync();
        await rival.ProductVariants.Where(v => v.Id == SecondVariantId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, 0));
        var placing = Record.ExceptionAsync(() => PlaceAsync(setup, token, expectedTotal: 4500));
        await WaitUntilSomeoneWaitsForALockAsync(setup);
        await rivalTransaction.CommitAsync();
        var exception = await placing;

        var conflict = Assert.IsType<ConflictException>(exception);
        Assert.Equal("order.stock_changed", conflict.Code);
        Assert.Equal(5, await StockOfAsync(setup, FirstVariantId));
        Assert.Equal(0, await OrderCountAsync(setup));
        var cart = await InScopeAsync(setup, s => s.Cart.GetAsync(token, default));
        Assert.Equal(2, cart.Lines.Count);
    }

    [Fact]
    public async Task Place_ManyBuyersForTheLastPiece_OnlyOneOrderIsMade()
    {
        await using var setup = await CreateAsync(firstStock: 1);
        var tokens = new List<string?>();
        for (var buyer = 0; buyer < 8; buyer++)
        {
            tokens.Add(await CartWithAsync(setup, (FirstVariantId, 1)));
        }

        var start = new TaskCompletionSource();
        var buyers = tokens.Select(token => Task.Run(async () =>
        {
            await start.Task;
            return await Record.ExceptionAsync(() => PlaceAsync(setup, token, expectedTotal: 1500));
        })).ToList();
        start.SetResult();
        var results = await Task.WhenAll(buyers);

        Assert.Single(results, exception => exception is null);
        Assert.All(
            results.Where(exception => exception is not null),
            exception => Assert.True(exception is ConflictException { Code: "order.stock_changed" }
                or RuleViolationException { Code: "order.cart_not_orderable" }));
        Assert.Equal(1, await OrderCountAsync(setup));
        Assert.Equal(0, await StockOfAsync(setup, FirstVariantId));
    }

    [Fact]
    public async Task ReleaseStock_GivesTheStockBackOnce()
    {
        await using var setup = await CreateAsync(firstStock: 5);
        var token = await CartWithAsync(setup, (FirstVariantId, 2));
        await PlaceAsync(setup, token, expectedTotal: 3000);
        var orderId = await OnlyOrderIdAsync(setup);
        Assert.Equal(3, await StockOfAsync(setup, FirstVariantId));

        await InScopeAsync(setup, async s =>
        {
            await s.Orders.ReleaseStockAsync(orderId, default);
            await s.Orders.ReleaseStockAsync(orderId, default);
            return true;
        });

        Assert.Equal(5, await StockOfAsync(setup, FirstVariantId));
        await using var context = setup.Database.CreateContext();
        Assert.NotNull(await context.Orders.AsNoTracking()
            .Where(o => o.Id == orderId).Select(o => o.StockReleasedAt).SingleAsync());
    }

    [Fact]
    public async Task ReleaseStock_SkipsAVariantThatHasNoStockFigureAnymore()
    {
        await using var setup = await CreateAsync(firstStock: 5, secondStock: 5);
        var token = await CartWithAsync(setup, (FirstVariantId, 2), (SecondVariantId, 1));
        await PlaceAsync(setup, token, expectedTotal: 4500);
        var orderId = await OnlyOrderIdAsync(setup);
        await using (var context = setup.Database.CreateContext())
        {
            // The product of the second line is made to order now, so its variant has no stock figure.
            await context.ProductVariants.Where(v => v.Id == SecondVariantId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, (int?)null));
        }

        await InScopeAsync(setup, async s =>
        {
            await s.Orders.ReleaseStockAsync(orderId, default);
            return true;
        });

        Assert.Equal(5, await StockOfAsync(setup, FirstVariantId));
        Assert.Null(await StockOfAsync(setup, SecondVariantId));
    }

    [Fact]
    public async Task ReleaseStock_OfAnUnknownOrder_IsNotFound()
    {
        await using var setup = await CreateAsync(firstStock: 5);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            InScopeAsync(setup, async s =>
            {
                await s.Orders.ReleaseStockAsync(Guid.NewGuid(), default);
                return true;
            }));

        Assert.Equal("order.not_found", exception.Code);
    }

    private static async Task WaitUntilSomeoneWaitsForALockAsync(Setup setup)
    {
        await using var context = setup.Database.CreateContext();
        var until = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < until)
        {
            var waiting = await context.Database
                .SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND datname = current_database()")
                .SingleAsync();
            if (waiting > 0)
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("The order never waited for the stock row.");
    }
}
