using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Application.Carts;
using Storefront.Application.Contracts;
using Storefront.Domain.Carts;

namespace Storefront.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class CartCleanupDatabaseTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static async Task<Guid> SeedVariantAsync(TestDatabase database)
    {
        await using var context = database.CreateContext();
        var category = CatalogData.Category();
        var product = CatalogData.Product(category.Id);
        var variant = CatalogData.Variant(product);
        context.Add(category);
        context.Add(product);
        await context.SaveChangesAsync();
        return variant.Id;
    }

    /// <summary>Saves a cart whose last change was <paramref name="idle"/> ago.</summary>
    private static async Task<Guid> SeedCartAsync(TestDatabase database, Guid variantId, TimeSpan idle, bool hasItem)
    {
        await using var context = database.CreateContext(new FakeTimeProvider(Now - idle));
        var cart = new Cart { TokenHash = Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray() };
        if (hasItem)
        {
            cart.Items.Add(new CartItem { CartId = cart.Id, VariantId = variantId, Quantity = 1 });
        }

        context.Add(cart);
        await context.SaveChangesAsync();
        return cart.Id;
    }

    private static async Task<int> CleanAsync(ApiFactory factory, int batchSize)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var repositories = scope.ServiceProvider.GetRequiredService<IRepositoryManager>();
        return await new CartCleanupService(repositories, new FakeTimeProvider(Now), batchSize).DeleteStaleAsync(default);
    }

    [Fact]
    public async Task OldCarts_AreDeletedWithTheirLines_AndTheOthersStayUntouched()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        var variantId = await SeedVariantAsync(database);
        var oldWithItem = await SeedCartAsync(database, variantId, TimeSpan.FromDays(31), hasItem: true);
        var oldEmpty = await SeedCartAsync(database, variantId, TimeSpan.FromDays(31), hasItem: false);
        var eightDaysEmpty = await SeedCartAsync(database, variantId, TimeSpan.FromDays(8), hasItem: false);
        var eightDaysWithItem = await SeedCartAsync(database, variantId, TimeSpan.FromDays(8), hasItem: true);
        var freshEmpty = await SeedCartAsync(database, variantId, TimeSpan.FromHours(1), hasItem: false);
        var freshWithItem = await SeedCartAsync(database, variantId, TimeSpan.FromHours(1), hasItem: true);

        var deleted = await CleanAsync(factory, batchSize: 500);

        Assert.Equal(3, deleted);
        await using var context = database.CreateContext();
        var left = await context.Carts.AsNoTracking().Select(c => c.Id).ToListAsync();
        Assert.Equivalent(new[] { eightDaysWithItem, freshEmpty, freshWithItem }, left);
        Assert.DoesNotContain(oldWithItem, left);
        Assert.DoesNotContain(oldEmpty, left);
        Assert.DoesNotContain(eightDaysEmpty, left);
        var lineOwners = await context.CartItems.AsNoTracking().Select(i => i.CartId).ToListAsync();
        Assert.Equivalent(new[] { eightDaysWithItem, freshWithItem }, lineOwners);
    }

    [Fact]
    public async Task ManyOldCarts_AreDeletedInSeveralBatches()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        var variantId = await SeedVariantAsync(database);
        for (var i = 0; i < 25; i++)
        {
            await SeedCartAsync(database, variantId, TimeSpan.FromDays(40), hasItem: i % 2 == 0);
        }

        var keep = await SeedCartAsync(database, variantId, TimeSpan.FromDays(2), hasItem: true);

        var deleted = await CleanAsync(factory, batchSize: 10);

        Assert.Equal(25, deleted);
        await using var context = database.CreateContext();
        Assert.Equal(keep, (await context.Carts.AsNoTracking().SingleAsync()).Id);
        Assert.Single(await context.CartItems.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AFreshCart_IsNotTouched_WhenThereIsNothingToDelete()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database.ConnectionString);
        var variantId = await SeedVariantAsync(database);
        await SeedCartAsync(database, variantId, TimeSpan.FromDays(1), hasItem: true);

        var deleted = await CleanAsync(factory, batchSize: 10);

        Assert.Equal(0, deleted);
        await using var context = database.CreateContext();
        Assert.Single(await context.Carts.AsNoTracking().ToListAsync());
    }
}
