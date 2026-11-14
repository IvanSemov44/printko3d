using Microsoft.EntityFrameworkCore;
using Npgsql;
using Storefront.Domain.Catalog;
using Storefront.Domain.Orders;

namespace Storefront.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class OrderConstraintTests(PostgresFixture postgres)
{
    private static async Task<string?> SqlStateOfAsync(Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);
        var postgresException = exception as PostgresException ?? exception?.InnerException as PostgresException;
        return postgresException?.SqlState;
    }

    private static Order NewOrder(int number = 12345678) => new()
    {
        Number = number,
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

    private static OrderItem NewItem(Guid orderId, Guid variantId, int quantity = 2, long unitPrice = 1500) => new()
    {
        OrderId = orderId,
        VariantId = variantId,
        ProductName = "Dragon",
        VariantName = "Blue",
        Sku = "DRG-001",
        UnitPrice = unitPrice,
        Quantity = quantity,
        LineTotal = unitPrice * quantity,
        FulfilmentType = FulfilmentType.InStock,
    };

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

    private static async Task<Guid> SeedOrderAsync(TestDatabase database, int number = 12345678)
    {
        await using var context = database.CreateContext();
        var order = NewOrder(number);
        context.Add(order);
        await context.SaveChangesAsync();
        return order.Id;
    }

    [Fact]
    public async Task Order_RejectsDuplicateNumber_AndAcceptsDistinctNumber()
    {
        var database = await postgres.CreateDatabaseAsync();
        await SeedOrderAsync(database);

        await using var duplicate = database.CreateContext();
        duplicate.Add(NewOrder());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, await SqlStateOfAsync(() => duplicate.SaveChangesAsync()));

        await using var distinct = database.CreateContext();
        distinct.Add(NewOrder(87654321));
        Assert.Equal(1, await distinct.SaveChangesAsync());
    }

    [Theory]
    [InlineData(9_999_999, false)]
    [InlineData(10_000_000, true)]
    [InlineData(99_999_999, true)]
    [InlineData(100_000_000, false)]
    [InlineData(0, false)]
    public async Task Order_AcceptsAnEightDigitNumber_Only(int number, bool isAccepted)
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        context.Add(NewOrder(number));

        var state = await SqlStateOfAsync(() => context.SaveChangesAsync());

        Assert.Equal(isAccepted ? null : PostgresErrorCodes.CheckViolation, state);
    }

    [Theory]
    [InlineData(-1, 3000, false)]
    [InlineData(0, 3000, true)]
    [InlineData(700, 3000, true)]
    [InlineData(3000, 3000, true)]
    [InlineData(3001, 3000, false)]
    public async Task Order_AcceptsADeliveryThatIsNotNegativeAndFitsInTheTotal_Only(
        long shippingAmount,
        long total,
        bool isAccepted)
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var order = NewOrder();
        order.ShippingMethod = "address";
        order.ShippingAmount = shippingAmount;
        order.TotalAmount = total;
        context.Add(order);

        var state = await SqlStateOfAsync(() => context.SaveChangesAsync());

        Assert.Equal(isAccepted ? null : PostgresErrorCodes.CheckViolation, state);
    }

    [Fact]
    public async Task Order_RejectsNegativeTotal()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var order = NewOrder();
        order.TotalAmount = -1;
        context.Add(order);

        Assert.Equal(PostgresErrorCodes.CheckViolation, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Theory]
    [InlineData("BG", PostgresErrorCodes.CheckViolation)]
    [InlineData("BGNN", PostgresErrorCodes.StringDataRightTruncation)]
    public async Task Order_RejectsCurrencyThatIsNotThreeLetters(string currency, string expectedState)
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var order = NewOrder();
        order.Currency = currency;
        context.Add(order);

        Assert.Equal(expectedState, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Theory]
    [InlineData("B", PostgresErrorCodes.CheckViolation)]
    [InlineData("BGR", PostgresErrorCodes.StringDataRightTruncation)]
    public async Task Order_RejectsCountryThatIsNotTwoLetters(string country, string expectedState)
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var order = NewOrder();
        order.ShippingCountry = country;
        context.Add(order);

        Assert.Equal(expectedState, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Fact]
    public async Task Order_StartsAsNew_AndStoresTheStatusAsText()
    {
        var database = await postgres.CreateDatabaseAsync();
        var orderId = await SeedOrderAsync(database);

        await using var context = database.CreateContext();
        var status = await context.Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM orders WHERE id = {orderId}")
            .SingleAsync();

        Assert.Equal("New", status);
    }

    [Fact]
    public async Task Order_ChangesItsRowVersion_OnEveryUpdate()
    {
        var database = await postgres.CreateDatabaseAsync();
        var orderId = await SeedOrderAsync(database);

        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var firstCopy = await first.Orders.SingleAsync(o => o.Id == orderId);
        var secondCopy = await second.Orders.SingleAsync(o => o.Id == orderId);
        firstCopy.CustomerName = "Changed once";
        await first.SaveChangesAsync();

        secondCopy.CustomerName = "Changed twice";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task OrderItem_RejectsUnknownOrderAndUnknownVariant()
    {
        var database = await postgres.CreateDatabaseAsync();
        var variantId = await SeedVariantAsync(database);
        var orderId = await SeedOrderAsync(database);

        await using var unknownOrder = database.CreateContext();
        unknownOrder.Add(NewItem(Guid.NewGuid(), variantId));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await SqlStateOfAsync(() => unknownOrder.SaveChangesAsync()));

        await using var unknownVariant = database.CreateContext();
        unknownVariant.Add(NewItem(orderId, Guid.NewGuid()));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await SqlStateOfAsync(() => unknownVariant.SaveChangesAsync()));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    public async Task OrderItem_AcceptsAPositiveQuantity_Only(int quantity, bool isAccepted)
    {
        var database = await postgres.CreateDatabaseAsync();
        var variantId = await SeedVariantAsync(database);
        var orderId = await SeedOrderAsync(database);
        await using var context = database.CreateContext();
        context.Add(NewItem(orderId, variantId, quantity));

        var state = await SqlStateOfAsync(() => context.SaveChangesAsync());

        Assert.Equal(isAccepted ? null : PostgresErrorCodes.CheckViolation, state);
    }

    [Fact]
    public async Task OrderItem_RejectsALineTotalThatIsNotPriceTimesQuantity()
    {
        var database = await postgres.CreateDatabaseAsync();
        var variantId = await SeedVariantAsync(database);
        var orderId = await SeedOrderAsync(database);
        await using var context = database.CreateContext();
        var item = NewItem(orderId, variantId, quantity: 2, unitPrice: 1500);
        item.LineTotal = 2999;
        context.Add(item);

        Assert.Equal(PostgresErrorCodes.CheckViolation, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Fact]
    public async Task OrderItem_RejectsNegativePrice()
    {
        var database = await postgres.CreateDatabaseAsync();
        var variantId = await SeedVariantAsync(database);
        var orderId = await SeedOrderAsync(database);
        await using var context = database.CreateContext();
        context.Add(NewItem(orderId, variantId, quantity: 1, unitPrice: -100));

        Assert.Equal(PostgresErrorCodes.CheckViolation, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Fact]
    public async Task OrderItem_AcceptsTheSameVariantTwiceInOneOrder()
    {
        // The service merges lines, but the table must not forbid two lines for one variant: the copy is what was sold.
        var database = await postgres.CreateDatabaseAsync();
        var variantId = await SeedVariantAsync(database);
        var orderId = await SeedOrderAsync(database);
        await using var context = database.CreateContext();
        context.Add(NewItem(orderId, variantId));
        context.Add(NewItem(orderId, variantId));

        Assert.Equal(2, await context.SaveChangesAsync());
    }

    [Fact]
    public async Task Variant_WithAnOrder_CannotBeDeleted()
    {
        var database = await postgres.CreateDatabaseAsync();
        var variantId = await SeedVariantAsync(database);
        var orderId = await SeedOrderAsync(database);
        await using (var context = database.CreateContext())
        {
            context.Add(NewItem(orderId, variantId));
            await context.SaveChangesAsync();
        }

        await using var deleting = database.CreateContext();
        var state = await SqlStateOfAsync(() =>
            deleting.ProductVariants.Where(v => v.Id == variantId).ExecuteDeleteAsync());

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, state);
    }

    [Fact]
    public async Task DeletingAnOrder_DeletesItsLines()
    {
        var database = await postgres.CreateDatabaseAsync();
        var variantId = await SeedVariantAsync(database);
        var orderId = await SeedOrderAsync(database);
        await using (var context = database.CreateContext())
        {
            context.Add(NewItem(orderId, variantId));
            await context.SaveChangesAsync();
        }

        await using var deleting = database.CreateContext();
        await deleting.Orders.Where(o => o.Id == orderId).ExecuteDeleteAsync();

        await using var reading = database.CreateContext();
        Assert.Empty(await reading.OrderItems.ToListAsync());
    }

    [Fact]
    public async Task OrderItem_KeepsTheCopiedPriceAndName_WhenTheVariantChangesLater()
    {
        var database = await postgres.CreateDatabaseAsync();
        var variantId = await SeedVariantAsync(database);
        var orderId = await SeedOrderAsync(database);
        await using (var context = database.CreateContext())
        {
            context.Add(NewItem(orderId, variantId, quantity: 2, unitPrice: 1500));
            await context.SaveChangesAsync();
        }

        await using (var changing = database.CreateContext())
        {
            await changing.ProductVariants.Where(v => v.Id == variantId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Price, 9900).SetProperty(v => v.Name, "Renamed"));
        }

        await using var reading = database.CreateContext();
        var item = await reading.OrderItems.SingleAsync();
        Assert.Equal((1500L, 3000L, "Blue"), (item.UnitPrice, item.LineTotal, item.VariantName));
    }
}
