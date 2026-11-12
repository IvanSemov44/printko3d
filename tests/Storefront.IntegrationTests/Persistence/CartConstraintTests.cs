using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Storefront.Domain.Carts;

namespace Storefront.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class CartConstraintTests(PostgresFixture postgres)
{
    private static async Task<string?> SqlStateOfAsync(Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);
        var postgresException = exception as PostgresException ?? exception?.InnerException as PostgresException;
        return postgresException?.SqlState;
    }

    private static Cart NewCart(byte[]? hash = null) =>
        new() { TokenHash = hash ?? SHA256.HashData(Guid.NewGuid().ToByteArray()) };

    private static async Task<Guid> SeedVariantAsync(TestDatabase database, string sku = "DRG-001", string slug = "dragon")
    {
        await using var context = database.CreateContext();
        var category = CatalogData.Category(slug);
        var product = CatalogData.Product(category.Id, slug);
        var variant = CatalogData.Variant(product, sku);
        context.Add(category);
        context.Add(product);
        await context.SaveChangesAsync();
        return variant.Id;
    }

    private static async Task<Guid> SeedCartAsync(TestDatabase database)
    {
        await using var context = database.CreateContext();
        var cart = NewCart();
        context.Add(cart);
        await context.SaveChangesAsync();
        return cart.Id;
    }

    [Fact]
    public async Task Cart_RejectsDuplicateTokenHash_AndAcceptsDistinctHash()
    {
        var database = await postgres.CreateDatabaseAsync();
        var hash = SHA256.HashData([1, 2, 3]);
        await using (var context = database.CreateContext())
        {
            context.Add(NewCart(hash));
            await context.SaveChangesAsync();
        }

        await using var duplicate = database.CreateContext();
        duplicate.Add(NewCart(hash));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, await SqlStateOfAsync(() => duplicate.SaveChangesAsync()));

        await using var distinct = database.CreateContext();
        distinct.Add(NewCart(SHA256.HashData([4, 5, 6])));
        Assert.Equal(1, await distinct.SaveChangesAsync());
    }

    [Theory]
    [InlineData(31)]
    [InlineData(33)]
    public async Task Cart_RejectsHashThatIsNotAnSha256Digest(int length)
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        context.Add(NewCart(new byte[length]));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Fact]
    public async Task CartItem_RejectsSameVariantTwiceInOneCart_AndAcceptsItInAnotherCart()
    {
        var database = await postgres.CreateDatabaseAsync();
        var variantId = await SeedVariantAsync(database);
        var cartId = await SeedCartAsync(database);
        await using (var context = database.CreateContext())
        {
            context.Add(new CartItem { CartId = cartId, VariantId = variantId, Quantity = 1 });
            await context.SaveChangesAsync();
        }

        await using var duplicate = database.CreateContext();
        duplicate.Add(new CartItem { CartId = cartId, VariantId = variantId, Quantity = 2 });
        Assert.Equal(PostgresErrorCodes.UniqueViolation, await SqlStateOfAsync(() => duplicate.SaveChangesAsync()));

        var otherCartId = await SeedCartAsync(database);
        await using var other = database.CreateContext();
        other.Add(new CartItem { CartId = otherCartId, VariantId = variantId, Quantity = 2 });
        Assert.Equal(1, await other.SaveChangesAsync());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    [InlineData(-1, false)]
    public async Task CartItem_AcceptsQuantityFromOneToTwenty_Only(int quantity, bool isAccepted)
    {
        var database = await postgres.CreateDatabaseAsync();
        var variantId = await SeedVariantAsync(database);
        var cartId = await SeedCartAsync(database);

        await using var context = database.CreateContext();
        context.Add(new CartItem { CartId = cartId, VariantId = variantId, Quantity = quantity });
        var state = await SqlStateOfAsync(() => context.SaveChangesAsync());

        Assert.Equal(isAccepted ? null : PostgresErrorCodes.CheckViolation, state);
    }

    [Fact]
    public async Task CartItem_RejectsUnknownCartAndUnknownVariant()
    {
        var database = await postgres.CreateDatabaseAsync();
        var variantId = await SeedVariantAsync(database);
        var cartId = await SeedCartAsync(database);

        await using var unknownCart = database.CreateContext();
        unknownCart.Add(new CartItem { CartId = Guid.NewGuid(), VariantId = variantId, Quantity = 1 });
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await SqlStateOfAsync(() => unknownCart.SaveChangesAsync()));

        await using var unknownVariant = database.CreateContext();
        unknownVariant.Add(new CartItem { CartId = cartId, VariantId = Guid.NewGuid(), Quantity = 1 });
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await SqlStateOfAsync(() => unknownVariant.SaveChangesAsync()));
    }

    [Fact]
    public async Task Variant_InACart_CannotBeDeleted()
    {
        var database = await postgres.CreateDatabaseAsync();
        var variantId = await SeedVariantAsync(database);
        var cartId = await SeedCartAsync(database);
        await using (var context = database.CreateContext())
        {
            context.Add(new CartItem { CartId = cartId, VariantId = variantId, Quantity = 1 });
            await context.SaveChangesAsync();
        }

        await using var deleting = database.CreateContext();
        var state = await SqlStateOfAsync(() =>
            deleting.ProductVariants.Where(v => v.Id == variantId).ExecuteDeleteAsync());

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, state);
    }

    [Fact]
    public async Task DeletingACart_DeletesItsLines()
    {
        var database = await postgres.CreateDatabaseAsync();
        var variantId = await SeedVariantAsync(database);
        var cartId = await SeedCartAsync(database);
        await using (var context = database.CreateContext())
        {
            context.Add(new CartItem { CartId = cartId, VariantId = variantId, Quantity = 1 });
            await context.SaveChangesAsync();
        }

        await using var deleting = database.CreateContext();
        await deleting.Carts.Where(c => c.Id == cartId).ExecuteDeleteAsync();

        await using var reading = database.CreateContext();
        Assert.Empty(await reading.CartItems.ToListAsync());
    }
}
