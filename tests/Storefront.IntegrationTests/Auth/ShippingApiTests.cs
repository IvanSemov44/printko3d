using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storefront.Domain.Catalog;
using Storefront.Domain.Shipping;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

/// <summary>
/// The delivery of the cart (task 25): the quotes and the order with the delivery in its total. The variant weighs 100 g
/// and costs 1500, the cart has two units: 200 g and 3000. The tariff is up to 500 g for 700 and up to 1000 g for 1200.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ShippingApiTests(PostgresFixture postgres)
{
    private const string QuotesUrl = "/api/v1/shipping/quotes";
    private const string OrdersUrl = "/api/v1/orders";
    private const string ItemsUrl = "/api/v1/cart/items";
    private const string CookieName = "__Host-printko-cart";

    private static ShippingRate Step(int maxWeightGrams, long price) =>
        new() { MethodCode = "address", MaxWeightGrams = maxWeightGrams, Price = price };

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string> CodeOf(HttpResponseMessage response) =>
        (await JsonOf(response)).GetProperty("code").GetString()!;

    private static async Task<(AuthApiHost Host, string Cookie)> HostWithACartAsync(
        PostgresFixture postgres,
        params ShippingRate[] tariff)
    {
        var host = await AuthApiHost.CreateAsync(postgres);
        await using (var context = host.Database.CreateContext())
        {
            var category = CatalogData.Category();
            var product = CatalogData.Product(category.Id);
            product.Status = ProductStatus.Published;
            var variant = CatalogData.Variant(product);
            variant.StockQuantity = 100;
            context.Add(category);
            context.Add(product);
            context.Add(CatalogData.Settings());
            context.AddRange(tariff);
            await context.SaveChangesAsync();

            var response = await host.SendAsync(HttpMethod.Post, ItemsUrl, new { variantId = variant.Id, quantity = 2 });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var cookie = response.Headers.GetValues("Set-Cookie")
                .First(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal)).Split(';')[0];
            return (host, cookie);
        }
    }

    private static object Order(long expectedTotal, string? shippingMethod = "address") => new
    {
        name = "Zlatko Testov",
        email = "zlatko.testov@example.test",
        phone = "+359 88 123 4567",
        street = "12 Imaginary Street",
        city = "Sofia",
        postalCode = "1000",
        termsAccepted = true,
        shippingMethod,
        expectedTotal,
    };

    private static async Task<int> OrderCountAsync(AuthApiHost host)
    {
        await using var context = host.Database.CreateContext();
        return await context.Orders.CountAsync();
    }

    [Fact]
    public async Task Quotes_ListTheMethodsWithTheirPrice_ForTheWeightOfTheCart()
    {
        var (host, cookie) = await HostWithACartAsync(postgres, Step(500, 700), Step(1000, 1200));
        await using var owner = host;

        var response = await host.SendAsync(HttpMethod.Get, QuotesUrl, null, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonOf(response);
        Assert.Equal(3000, body.GetProperty("itemsTotal").GetInt64());
        var method = Assert.Single(body.GetProperty("methods").EnumerateArray());
        Assert.Equal(("address", 700), (method.GetProperty("code").GetString(), method.GetProperty("amount").GetInt64()));
    }

    [Fact]
    public async Task Quotes_DoNotChangeTheCartContract_WeightStaysOnTheServer()
    {
        var (host, cookie) = await HostWithACartAsync(postgres, Step(500, 700));
        await using var owner = host;

        var cart = await JsonOf(await host.SendAsync(HttpMethod.Get, "/api/v1/cart", null, cookie));

        Assert.False(cart.GetProperty("lines")[0].TryGetProperty("weightGrams", out _));
    }

    [Fact]
    public async Task Quotes_AreEmpty_WhenNoTariffIsSetUp()
    {
        var (host, cookie) = await HostWithACartAsync(postgres);
        await using var owner = host;

        var response = await host.SendAsync(HttpMethod.Get, QuotesUrl, null, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await JsonOf(response)).GetProperty("methods").EnumerateArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Quotes_WithoutACartOrWithAnUnknownCookie_Are422_CartEmpty(bool withUnknownCookie)
    {
        var (host, _) = await HostWithACartAsync(postgres, Step(500, 700));
        await using var owner = host;

        var cookie = withUnknownCookie ? $"{CookieName}=unknown-value" : null;
        var response = await host.SendAsync(HttpMethod.Get, QuotesUrl, null, cookie);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("order.cart_empty", await CodeOf(response));
    }

    [Fact]
    public async Task Quotes_ForACartHeavierThanTheTariff_Are422_TooHeavy()
    {
        var (host, cookie) = await HostWithACartAsync(postgres, Step(150, 700));
        await using var owner = host;

        var response = await host.SendAsync(HttpMethod.Get, QuotesUrl, null, cookie);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("shipping.too_heavy", await CodeOf(response));
    }

    [Fact]
    public async Task Order_AddsTheDeliveryToTheTotal_AndKeepsACopyOfMethodAndPrice()
    {
        var (host, cookie) = await HostWithACartAsync(postgres, Step(500, 700), Step(1000, 1200));
        await using var owner = host;

        var response = await host.SendAsync(HttpMethod.Post, OrdersUrl, Order(expectedTotal: 3700), cookie);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await JsonOf(response);
        Assert.Equal(
            (3700, 700, "address"),
            (order.GetProperty("total").GetInt64(), order.GetProperty("shippingAmount").GetInt64(), order.GetProperty("shippingMethod").GetString()));
        await using var context = host.Database.CreateContext();
        var saved = await context.Orders.SingleAsync();
        Assert.Equal((3700L, 700L, "address"), (saved.TotalAmount, saved.ShippingAmount, saved.ShippingMethod));
    }

    [Fact]
    public async Task Order_ThatForgetsTheDelivery_IsTheTotalChanged_AndCreatesNothing()
    {
        var (host, cookie) = await HostWithACartAsync(postgres, Step(500, 700));
        await using var owner = host;

        var response = await host.SendAsync(HttpMethod.Post, OrdersUrl, Order(expectedTotal: 3000), cookie);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("order.total_changed", await CodeOf(response));
        Assert.Equal(0, await OrderCountAsync(host));
    }

    [Fact]
    public async Task Order_WhenTheTariffChangesAfterThePreview_IsTheTotalChanged()
    {
        var (host, cookie) = await HostWithACartAsync(postgres, Step(500, 700));
        await using var owner = host;
        await using (var context = host.Database.CreateContext())
        {
            await context.ShippingRates.ExecuteUpdateAsync(s => s.SetProperty(r => r.Price, 900L));
        }

        var response = await host.SendAsync(HttpMethod.Post, OrdersUrl, Order(expectedTotal: 3700), cookie);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("order.total_changed", await CodeOf(response));
    }

    [Fact]
    public async Task Order_WithAnUnknownMethod_Is422_ShippingUnavailable()
    {
        var (host, cookie) = await HostWithACartAsync(postgres, Step(500, 700));
        await using var owner = host;

        var response = await host.SendAsync(HttpMethod.Post, OrdersUrl, Order(3700, shippingMethod: "teleport"), cookie);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("order.shipping_unavailable", await CodeOf(response));
        Assert.Equal(0, await OrderCountAsync(host));
    }

    [Fact]
    public async Task Order_WithoutAMethod_Is400_NamingTheField()
    {
        var (host, cookie) = await HostWithACartAsync(postgres, Step(500, 700));
        await using var owner = host;

        var response = await host.SendAsync(HttpMethod.Post, OrdersUrl, Order(3700, shippingMethod: ""), cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonOf(response)).GetProperty("errors").TryGetProperty("shippingMethod", out _));
    }

    [Fact]
    public async Task Order_ForAParcelHeavierThanTheTariff_Is422_TooHeavy_AndLeavesTheCartAlone()
    {
        var (host, cookie) = await HostWithACartAsync(postgres, Step(150, 700));
        await using var owner = host;

        var response = await host.SendAsync(HttpMethod.Post, OrdersUrl, Order(3700), cookie);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("shipping.too_heavy", await CodeOf(response));
        Assert.Equal(0, await OrderCountAsync(host));
        var cart = await JsonOf(await host.SendAsync(HttpMethod.Get, "/api/v1/cart", null, cookie));
        Assert.Equal(2, cart.GetProperty("itemCount").GetInt32());
    }

    [Fact]
    public async Task OldOrder_KeepsItsDelivery_WhenTheTariffChangesLater()
    {
        var (host, cookie) = await HostWithACartAsync(postgres, Step(500, 700));
        await using var owner = host;
        await host.SendAsync(HttpMethod.Post, OrdersUrl, Order(3700), cookie);

        await using (var context = host.Database.CreateContext())
        {
            await context.ShippingRates.ExecuteUpdateAsync(s => s.SetProperty(r => r.Price, 5000L));
        }

        await using var read = host.Database.CreateContext();
        var saved = await read.Orders.SingleAsync();
        Assert.Equal((3700L, 700L), (saved.TotalAmount, saved.ShippingAmount));
    }
}
