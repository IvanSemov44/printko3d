using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Storefront.Domain.Catalog;
using Storefront.Domain.Orders;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class OrdersApiTests(PostgresFixture postgres)
{
    private const string OrdersUrl = "/api/v1/orders";
    private const string CartUrl = "/api/v1/cart";
    private const string ItemsUrl = "/api/v1/cart/items";
    private const string CookieName = "__Host-printko-cart";

    // Made-up personal data; the log test looks for every one of them.
    private const string BuyerName = "Zlatko Testov";
    private const string BuyerEmail = "zlatko.testov@example.test";
    private const string BuyerPhone = "+359 88 123 4567";
    private const string BuyerStreet = "12 Imaginary Street";

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _entries = [];

        public string AllText()
        {
            lock (_entries)
            {
                return string.Join('\n', _entries);
            }
        }

        public ILogger CreateLogger(string categoryName) => new Capturing(this);

        public void Dispose()
        {
        }

        private sealed class Capturing(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (owner._entries)
                {
                    owner._entries.Add($"{formatter(state, exception)} {exception}");
                }
            }
        }
    }

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string CartCookieOf(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie")
            .First(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal))
            .Split(';')[0];

    private static async Task<Guid> SeedVariantAsync(AuthApiHost host, int stock = 100)
    {
        await using var context = host.Database.CreateContext();
        var category = CatalogData.Category();
        var product = CatalogData.Product(category.Id);
        product.Status = ProductStatus.Published;
        var variant = CatalogData.Variant(product);
        variant.StockQuantity = stock;
        context.Add(category);
        context.Add(product);
        context.Add(CatalogData.Settings());
        await context.SaveChangesAsync();
        return variant.Id;
    }

    /// <summary>A cart with two units of the variant (price 1500, so 3000), and the cookie that owns it.</summary>
    private static async Task<string> CartCookieWithAsync(AuthApiHost host, Guid variantId)
    {
        var response = await host.SendAsync(HttpMethod.Post, ItemsUrl, new { variantId, quantity = 2 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return CartCookieOf(response);
    }

    private static object Body(long expectedTotal = 3000, bool termsAccepted = true, string email = BuyerEmail) => new
    {
        name = BuyerName,
        email,
        phone = BuyerPhone,
        street = BuyerStreet,
        city = "Sofia",
        postalCode = "1000",
        termsAccepted,
        expectedTotal,
    };

    private static async Task<int> OrderCountAsync(AuthApiHost host)
    {
        await using var context = host.Database.CreateContext();
        return await context.Orders.CountAsync();
    }

    [Fact]
    public async Task Post_CreatesTheOrder_Answers201WithTheOrder_AndEmptiesTheCart()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var cookie = await CartCookieWithAsync(host, variantId);

        var response = await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(), cookie);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await JsonOf(response);
        Assert.InRange(order.GetProperty("number").GetInt32(), Order.MinNumber, Order.MaxNumber);
        Assert.Equal(("New", "BGN", 3000), (
            order.GetProperty("status").GetString(),
            order.GetProperty("currency").GetString(),
            order.GetProperty("total").GetInt32()));
        Assert.Equal(("Zlatko Testov", "BG"), (
            order.GetProperty("customerName").GetString(),
            order.GetProperty("shippingCountry").GetString()));
        var item = order.GetProperty("items")[0];
        Assert.Equal(("Dragon", "Blue", "DRG-001", 1500, 2, 3000, "InStock"), (
            item.GetProperty("productName").GetString(),
            item.GetProperty("variantName").GetString(),
            item.GetProperty("sku").GetString(),
            item.GetProperty("unitPrice").GetInt32(),
            item.GetProperty("quantity").GetInt32(),
            item.GetProperty("lineTotal").GetInt32(),
            item.GetProperty("fulfilmentType").GetString()));
        Assert.False(order.TryGetProperty("id", out _));
        Assert.Equal(1, await OrderCountAsync(host));

        var cart = await JsonOf(await host.SendAsync(HttpMethod.Get, CartUrl, cookie: cookie));
        Assert.Equal(0, cart.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task Post_WithoutTheCsrfHeader_Is403_AndCreatesNothing()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var cookie = await CartCookieWithAsync(host, variantId);

        var response = await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(), cookie, csrfHeader: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("csrf.header_required", (await JsonOf(response)).GetProperty("code").GetString());
        Assert.Equal(0, await OrderCountAsync(host));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Post_WithoutACartOrWithAnUnknownCookie_Is422_CartEmpty(bool withUnknownCookie)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedVariantAsync(host);
        var cookie = withUnknownCookie ? $"{CookieName}=unknown-value" : null;

        var response = await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(0), cookie);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("order.cart_empty", (await JsonOf(response)).GetProperty("code").GetString());
        Assert.Null(response.Headers.TryGetValues("Set-Cookie", out var values) ? values.FirstOrDefault() : null);
    }

    [Fact]
    public async Task Post_WithTheTotalChanged_Is409()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var cookie = await CartCookieWithAsync(host, variantId);

        var response = await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(expectedTotal: 2000), cookie);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("order.total_changed", (await JsonOf(response)).GetProperty("code").GetString());
        Assert.Equal(0, await OrderCountAsync(host));
    }

    [Fact]
    public async Task Post_WithALineThatCannotBeOrdered_Is422()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var cookie = await CartCookieWithAsync(host, variantId);
        await using (var context = host.Database.CreateContext())
        {
            await context.Products.ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, ProductStatus.Archived));
        }

        var response = await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(0), cookie);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("order.cart_not_orderable", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_WithInvalidFields_Is400_NamingTheFields()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var cookie = await CartCookieWithAsync(host, variantId);

        var response = await host.SendAsync(
            HttpMethod.Post, OrdersUrl, Body(termsAccepted: false, email: "not-an-email"), cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await JsonOf(response);
        Assert.Equal("validation.failed", problem.GetProperty("code").GetString());
        var fields = problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order();
        Assert.Equal(["email", "termsAccepted"], fields);
        Assert.Equal(0, await OrderCountAsync(host));
    }

    [Fact]
    public async Task Post_WithAMissingFieldOrNoBody_Is400()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var cookie = await CartCookieWithAsync(host, variantId);

        var missingField = await host.SendAsync(HttpMethod.Post, OrdersUrl, new { name = BuyerName }, cookie);
        var emptyObject = await host.SendAsync(HttpMethod.Post, OrdersUrl, new { }, cookie);

        Assert.Equal(HttpStatusCode.BadRequest, missingField.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyObject.StatusCode);
        Assert.Equal(0, await OrderCountAsync(host));
    }

    [Fact]
    public async Task Post_IsOpenToAnAnonymousBuyer_NoAdminSessionNeeded()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var cookie = await CartCookieWithAsync(host, variantId);

        var response = await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(), cookie);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Log_NeverHoldsTheBuyersNameEmailPhoneOrAddress()
    {
        var logs = new CapturingLoggerProvider();
        await using var host = await AuthApiHost.CreateAsync(
            postgres, configureServices: services => services.AddLogging(logging => logging.AddProvider(logs)));
        var variantId = await SeedVariantAsync(host);
        var cookie = await CartCookieWithAsync(host, variantId);

        // A refusal for the total, a refusal for the fields, then the order itself.
        await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(expectedTotal: 1), cookie);
        await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(termsAccepted: false), cookie);
        var placed = await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(), cookie);
        await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(), cookie);

        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var text = logs.AllText();
        Assert.NotEmpty(text);
        Assert.DoesNotContain(BuyerName, text, StringComparison.Ordinal);
        Assert.DoesNotContain(BuyerEmail, text, StringComparison.Ordinal);
        Assert.DoesNotContain(BuyerPhone, text, StringComparison.Ordinal);
        Assert.DoesNotContain(BuyerStreet, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_WhenTheStockChangedAtTheSameMoment_Answers409AndLogsTheRefusalWithoutPersonalData()
    {
        var logs = new CapturingLoggerProvider();
        await using var host = await AuthApiHost.CreateAsync(
            postgres, configureServices: services => services.AddLogging(logging => logging.AddProvider(logs)));
        var variantId = await SeedVariantAsync(host, stock: 5);
        var cookie = await CartCookieWithAsync(host, variantId);

        // Another buyer holds the variant row and takes the last pieces; the order waits for that row.
        await using var rival = host.Database.CreateContext();
        await using var rivalTransaction = await rival.Database.BeginTransactionAsync();
        await rival.ProductVariants.Where(v => v.Id == variantId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, 0));
        var placing = host.SendAsync(HttpMethod.Post, OrdersUrl, Body(), cookie);
        await WaitUntilSomeoneWaitsForALockAsync(host);
        await rivalTransaction.CommitAsync();
        var response = await placing;

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("order.stock_changed", (await JsonOf(response)).GetProperty("code").GetString());
        Assert.Equal(0, await OrderCountAsync(host));
        var text = logs.AllText();
        Assert.Contains("An order was refused because the stock changed at the same moment.", text, StringComparison.Ordinal);
        Assert.DoesNotContain(BuyerName, text, StringComparison.Ordinal);
        Assert.DoesNotContain(BuyerEmail, text, StringComparison.Ordinal);
        Assert.DoesNotContain(BuyerPhone, text, StringComparison.Ordinal);
        Assert.DoesNotContain(BuyerStreet, text, StringComparison.Ordinal);
    }

    private static async Task WaitUntilSomeoneWaitsForALockAsync(AuthApiHost host)
    {
        await using var context = host.Database.CreateContext();
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
