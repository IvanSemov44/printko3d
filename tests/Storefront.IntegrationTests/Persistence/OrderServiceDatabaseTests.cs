using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Storefront.Application.Carts.Queries;
using Storefront.Application.Catalog;
using Storefront.Application.Exceptions;
using Storefront.Application.Orders;
using Storefront.Domain.Catalog;
using Storefront.Domain.Orders;

namespace Storefront.IntegrationTests.Persistence;

/// <summary>The order service against a real PostgreSQL. Every call gets its own scope, like a separate request.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderServiceDatabaseTests(PostgresFixture postgres)
{
    private sealed class FixedNumbers(params int[] numbers) : IOrderNumberGenerator
    {
        private int _next;

        public int Next() => numbers[Math.Min(_next++, numbers.Length - 1)];
    }

    private sealed record Setup(TestDatabase Database, WebApplicationFactory<Program> Factory, Guid VariantId)
        : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }

    private static PlaceOrderInput Valid(long expectedTotal, Action<Fields>? change = null)
    {
        var fields = new Fields(expectedTotal);
        change?.Invoke(fields);
        return new PlaceOrderInput(
            fields.Name,
            fields.Email,
            fields.Phone,
            fields.Street,
            fields.City,
            fields.PostalCode,
            fields.TermsAccepted,
            fields.ShippingMethod,
            fields.ExpectedTotal);
    }

    private sealed class Fields(long expectedTotal)
    {
        public string Name { get; set; } = "Test Buyer";

        public string Email { get; set; } = "buyer@example.com";

        public string Phone { get; set; } = "+359 88 000 0000";

        public string Street { get; set; } = "1 Test Street";

        public string City { get; set; } = "Sofia";

        public string PostalCode { get; set; } = "1000";

        public bool TermsAccepted { get; set; } = true;

        public string ShippingMethod { get; set; } = "address";

        public long ExpectedTotal { get; set; } = expectedTotal;
    }

    private async Task<Setup> CreateAsync(
        ProductStatus status = ProductStatus.Published,
        FulfilmentType fulfilment = FulfilmentType.InStock,
        int? stock = 5,
        IOrderNumberGenerator? numbers = null)
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var category = CatalogData.Category();
        var product = CatalogData.Product(
            category.Id,
            fulfilmentType: fulfilment,
            leadTimeDays: fulfilment == FulfilmentType.MadeToOrder ? 7 : null);
        product.Status = status;
        var variant = CatalogData.Variant(product);
        variant.StockQuantity = stock;
        context.Add(category);
        context.Add(product);
        context.Add(CatalogData.Settings());
        context.Add(CatalogData.FreeShipping());
        await context.SaveChangesAsync();

        var factory = new ApiFactory(database.ConnectionString).WithWebHostBuilder(builder =>
        {
            if (numbers is not null)
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IOrderNumberGenerator>();
                    services.AddSingleton(numbers);
                });
            }
        });
        return new Setup(database, factory, variant.Id);
    }

    private static async Task<T> InScopeAsync<T>(Setup setup, Func<IServiceManager, Task<T>> action)
    {
        await using var scope = setup.Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IServiceManager>());
    }

    private static async Task<string?> CartWithAsync(Setup setup, int quantity = 2)
    {
        var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(null, setup.VariantId, quantity, default));
        return change.Token;
    }

    private static async Task<OrderDto> PlaceAsync(Setup setup, string? token, PlaceOrderInput input) =>
        await InScopeAsync(setup, s => s.Orders.PlaceAsync(token, input, default));

    private static async Task<int> OrderCountAsync(Setup setup)
    {
        await using var context = setup.Database.CreateContext();
        return await context.Orders.CountAsync();
    }

    private static async Task<int> CartLineCountAsync(Setup setup, string? token) =>
        (await InScopeAsync(setup, s => s.Cart.GetAsync(token, default))).Lines.Count;

    [Fact]
    public async Task Place_CopiesNameSkuPriceAndCurrency_AndEmptiesTheCart()
    {
        await using var setup = await CreateAsync();
        var token = await CartWithAsync(setup, quantity: 2);

        var order = await PlaceAsync(setup, token, Valid(3000));

        var item = Assert.Single(order.Items);
        Assert.Equal(
            ("New", "BGN", 3000L, "Dragon", "Blue", "DRG-001", 1500L, 2, 3000L),
            (order.Status, order.Currency, order.Total, item.ProductName, item.VariantName, item.Sku, item.UnitPrice, item.Quantity, item.LineTotal));
        Assert.InRange(order.Number, Order.MinNumber, Order.MaxNumber);
        Assert.Equal(0, await CartLineCountAsync(setup, token));
        await using var context = setup.Database.CreateContext();
        var stored = await context.Orders.Include(o => o.Items).SingleAsync();
        Assert.Equal(
            ("Test Buyer", "buyer@example.com", "1 Test Street", "Sofia", "1000", "BG", "draft-1"),
            (stored.CustomerName, stored.Email, stored.ShippingStreet, stored.ShippingCity, stored.ShippingPostalCode, stored.ShippingCountry, stored.TermsVersion));
        Assert.True(Math.Abs((DateTimeOffset.UtcNow - stored.TermsAcceptedAt).TotalMinutes) < 5);
    }

    [Fact]
    public async Task Place_KeepsTheCopiedPrice_WhenTheVariantChangesLater()
    {
        await using var setup = await CreateAsync();
        var token = await CartWithAsync(setup, quantity: 2);
        await PlaceAsync(setup, token, Valid(3000));

        await using (var context = setup.Database.CreateContext())
        {
            await context.ProductVariants
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Price, 9900).SetProperty(v => v.Name, "Renamed"));
        }

        await using var reading = setup.Database.CreateContext();
        var item = await reading.OrderItems.SingleAsync();
        Assert.Equal((1500L, 3000L, "Blue"), (item.UnitPrice, item.LineTotal, item.VariantName));
    }

    [Fact]
    public async Task Place_CopiesTheLeadTime_OfAMadeToOrderLine()
    {
        await using var setup = await CreateAsync(fulfilment: FulfilmentType.MadeToOrder, stock: null);
        var token = await CartWithAsync(setup, quantity: 1);

        var order = await PlaceAsync(setup, token, Valid(1500));

        var item = Assert.Single(order.Items);
        Assert.Equal((FulfilmentType.MadeToOrder, 7), (item.FulfilmentType, item.LeadTimeDays));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Place_RefusesAnEmptyCart(bool hasCartWithoutLines)
    {
        await using var setup = await CreateAsync();
        string? token = null;
        if (hasCartWithoutLines)
        {
            token = await CartWithAsync(setup);
            await InScopeAsync(setup, s => s.Cart.ClearAsync(token, default));
        }

        var exception = await Assert.ThrowsAsync<RuleViolationException>(() => PlaceAsync(setup, token, Valid(0)));

        Assert.Equal("order.cart_empty", exception.Code);
        Assert.Equal(0, await OrderCountAsync(setup));
    }

    [Theory]
    [InlineData("archived")]
    [InlineData("inactive")]
    [InlineData("stock")]
    public async Task Place_RefusesALineThatCannotBeOrdered_AndLeavesTheCartAsItWas(string reason)
    {
        await using var setup = await CreateAsync();
        var token = await CartWithAsync(setup, quantity: 2);
        await using (var context = setup.Database.CreateContext())
        {
            _ = reason switch
            {
                "archived" => await context.Products.ExecuteUpdateAsync(
                    s => s.SetProperty(p => p.Status, ProductStatus.Archived)),
                "inactive" => await context.ProductVariants.ExecuteUpdateAsync(
                    s => s.SetProperty(v => v.IsActive, false)),
                _ => await context.ProductVariants.ExecuteUpdateAsync(
                    s => s.SetProperty(v => v.StockQuantity, 1)),
            };
        }

        var exception = await Assert.ThrowsAsync<RuleViolationException>(() => PlaceAsync(setup, token, Valid(0)));

        Assert.Equal("order.cart_not_orderable", exception.Code);
        Assert.Equal(0, await OrderCountAsync(setup));
        Assert.Equal(1, await CartLineCountAsync(setup, token));
    }

    [Fact]
    public async Task Place_RefusesWhenTheTotalIsNotTheOneTheBuyerSaw()
    {
        await using var setup = await CreateAsync();
        var token = await CartWithAsync(setup, quantity: 2);
        await using (var context = setup.Database.CreateContext())
        {
            await context.ProductVariants.ExecuteUpdateAsync(s => s.SetProperty(v => v.Price, 1600));
        }

        var exception = await Assert.ThrowsAsync<ConflictException>(() => PlaceAsync(setup, token, Valid(3000)));

        Assert.Equal("order.total_changed", exception.Code);
        Assert.Equal(0, await OrderCountAsync(setup));
        Assert.Equal(1, await CartLineCountAsync(setup, token));
    }

    [Theory]
    [InlineData("name", "")]
    [InlineData("name", "   ")]
    [InlineData("email", "")]
    [InlineData("email", "not-an-email")]
    [InlineData("email", "buyer@example.com extra")]
    [InlineData("phone", "")]
    [InlineData("phone", "abc")]
    [InlineData("phone", "12345")]
    [InlineData("street", "")]
    [InlineData("city", "")]
    [InlineData("postalCode", "")]
    [InlineData("postalCode", "100")]
    [InlineData("postalCode", "10000")]
    [InlineData("postalCode", "10a0")]
    [InlineData("termsAccepted", "no")]
    public async Task Place_RefusesInvalidFields_ByName_AndChangesNothing(string field, string value)
    {
        await using var setup = await CreateAsync();
        var token = await CartWithAsync(setup, quantity: 2);
        var input = Valid(3000, f =>
        {
            switch (field)
            {
                case "name": f.Name = value; break;
                case "email": f.Email = value; break;
                case "phone": f.Phone = value; break;
                case "street": f.Street = value; break;
                case "city": f.City = value; break;
                case "postalCode": f.PostalCode = value; break;
                default: f.TermsAccepted = false; break;
            }
        });

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => PlaceAsync(setup, token, input));

        Assert.Contains(field, exception.Errors.Keys);
        Assert.Equal(0, await OrderCountAsync(setup));
        Assert.Equal(1, await CartLineCountAsync(setup, token));
    }

    [Fact]
    public async Task Place_RefusesTooLongFields()
    {
        await using var setup = await CreateAsync();
        var token = await CartWithAsync(setup, quantity: 2);
        var input = Valid(3000, f =>
        {
            f.Name = new string('n', 201);
            f.Street = new string('s', 201);
            f.City = new string('c', 101);
            f.Email = new string('e', 250) + "@x.bg";
        });

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => PlaceAsync(setup, token, input));

        Assert.Equal(["city", "email", "name", "street"], exception.Errors.Keys.Order());
    }

    [Fact]
    public async Task Place_TrimsTheFields()
    {
        await using var setup = await CreateAsync();
        var token = await CartWithAsync(setup, quantity: 2);

        var order = await PlaceAsync(setup, token, Valid(3000, f =>
        {
            f.Name = "  Test Buyer ";
            f.City = " Sofia  ";
        }));

        Assert.Equal(("Test Buyer", "Sofia"), (order.CustomerName, order.ShippingCity));
    }

    [Fact]
    public async Task Place_Twice_CreatesOneOrder_AndTheSecondFindsAnEmptyCart()
    {
        await using var setup = await CreateAsync();
        var token = await CartWithAsync(setup, quantity: 2);
        await PlaceAsync(setup, token, Valid(3000));

        var exception = await Assert.ThrowsAsync<RuleViolationException>(() => PlaceAsync(setup, token, Valid(3000)));

        Assert.Equal("order.cart_empty", exception.Code);
        Assert.Equal(1, await OrderCountAsync(setup));
    }

    [Fact]
    public async Task Place_AtTheSameMoment_CreatesOneOrder()
    {
        await using var setup = await CreateAsync();
        var token = await CartWithAsync(setup, quantity: 2);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 2).Select(_ => Record.ExceptionAsync(() => PlaceAsync(setup, token, Valid(3000)))));

        Assert.Equal(1, await OrderCountAsync(setup));
        Assert.Single(results, exception => exception is null);
        var refused = Assert.Single(results, exception => exception is not null);
        Assert.True(refused is RuleViolationException { Code: "order.cart_empty" }
            or ConflictException { Code: "cart.changed" });
    }

    [Fact]
    public async Task Place_TriesAnotherNumber_WhenTheFirstIsTaken()
    {
        await using var setup = await CreateAsync(numbers: new FixedNumbers(11111111, 11111111, 22222222));
        await using (var context = setup.Database.CreateContext())
        {
            context.Add(new Order
            {
                Number = 11111111,
                Currency = "BGN",
                CustomerName = "Other",
                Email = "other@example.com",
                Phone = "+359000000",
                ShippingStreet = "x",
                ShippingCity = "x",
                ShippingPostalCode = "1000",
                ShippingCountry = "BG",
                TermsVersion = "draft-1",
            });
            await context.SaveChangesAsync();
        }

        var token = await CartWithAsync(setup, quantity: 2);

        var order = await PlaceAsync(setup, token, Valid(3000));

        Assert.Equal(22222222, order.Number);
    }

    [Fact]
    public async Task Place_GivesUp_AfterFiveTakenNumbers_AndChangesNothing()
    {
        await using var setup = await CreateAsync(numbers: new FixedNumbers(11111111));
        await using (var context = setup.Database.CreateContext())
        {
            context.Add(new Order
            {
                Number = 11111111,
                Currency = "BGN",
                CustomerName = "Other",
                Email = "other@example.com",
                Phone = "+359000000",
                ShippingStreet = "x",
                ShippingCity = "x",
                ShippingPostalCode = "1000",
                ShippingCountry = "BG",
                TermsVersion = "draft-1",
            });
            await context.SaveChangesAsync();
        }

        var token = await CartWithAsync(setup, quantity: 2);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => PlaceAsync(setup, token, Valid(3000)));

        Assert.DoesNotContain("11111111", exception.Message);
        Assert.Equal(1, await OrderCountAsync(setup));
        Assert.Equal(1, await CartLineCountAsync(setup, token));
    }

    [Fact]
    public async Task Place_SumsSeveralLines()
    {
        await using var setup = await CreateAsync();
        Guid second;
        await using (var context = setup.Database.CreateContext())
        {
            var categoryId = (await context.Categories.AsNoTracking().FirstAsync()).Id;
            var product = CatalogData.Product(categoryId, "owl");
            product.Status = ProductStatus.Published;
            var variant = CatalogData.Variant(product, "OWL-001");
            variant.Price = 700;
            context.Add(product);
            await context.SaveChangesAsync();
            second = variant.Id;
        }

        var token = await CartWithAsync(setup, quantity: 2);
        await InScopeAsync(setup, s => s.Cart.AddItemAsync(token, second, 3, default));

        var order = await PlaceAsync(setup, token, Valid(2 * 1500 + 3 * 700));

        Assert.Equal((2, 5100L), (order.Items.Count, order.Total));
    }

    [Fact]
    public async Task RandomNumbers_StayInTheEightDigitRange()
    {
        var generator = new RandomOrderNumberGenerator();

        var numbers = Enumerable.Range(0, 1000).Select(_ => generator.Next()).ToList();

        Assert.All(numbers, number => Assert.InRange(number, Order.MinNumber, Order.MaxNumber));
        Assert.True(numbers.Distinct().Count() > 900);
    }
}
