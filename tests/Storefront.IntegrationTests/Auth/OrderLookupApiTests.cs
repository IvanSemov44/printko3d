using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storefront.Domain.Catalog;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

/// <summary>Finding an order again, and the limits of the open order addresses (task 27d, debt D64).</summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderLookupApiTests(PostgresFixture postgres)
{
    private const string OrdersUrl = "/api/v1/orders";
    private const string LookupUrl = "/api/v1/orders/lookup";
    private const string TokenUrl = "/api/v1/orders/lookup-by-token";
    private const string CookieName = "__Host-printko-cart";
    private const string BuyerEmail = "buyer.lookup@example.test";

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> SeedVariantAsync(AuthApiHost host)
    {
        await using var context = host.Database.CreateContext();
        var category = CatalogData.Category();
        var product = CatalogData.Product(category.Id);
        product.Status = ProductStatus.Published;
        var variant = CatalogData.Variant(product);
        variant.StockQuantity = 100;
        context.Add(category);
        context.Add(product);
        context.Add(CatalogData.Settings());
        context.Add(CatalogData.FreeShipping());
        await context.SaveChangesAsync();
        return variant.Id;
    }

    private static async Task<string> CartCookieWithAsync(AuthApiHost host, Guid variantId)
    {
        var response = await host.SendAsync(HttpMethod.Post, "/api/v1/cart/items", new { variantId, quantity = 2 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return response.Headers.GetValues("Set-Cookie")
            .First(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal))
            .Split(';')[0];
    }

    private static object Body() => new
    {
        name = "Lookup Buyer",
        email = BuyerEmail,
        phone = "+359 88 123 4567",
        street = "1 Imaginary Street",
        city = "Sofia",
        postalCode = "1000",
        termsAccepted = true,
        shippingMethod = "address",
        expectedTotal = 3000,
    };

    private async Task<(AuthApiHost Host, int Number, string Token)> PlacedAsync(IReadOnlyDictionary<string, string>? settings = null)
    {
        var host = await AuthApiHost.CreateAsync(postgres, extraSettings: settings);
        var cookie = await CartCookieWithAsync(host, await SeedVariantAsync(host));
        var placed = await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(), cookie);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var number = (await JsonOf(placed)).GetProperty("number").GetInt32();
        await using var context = host.Database.CreateContext();
        var token = await context.Orders.AsNoTracking().Where(o => o.Number == number).Select(o => o.AccessToken).SingleAsync();
        return (host, number, token!);
    }

    [Fact]
    public async Task Lookup_WithTheNumberAndTheEmail_Answers200WithTheOrder()
    {
        var (host, number, _) = await PlacedAsync();
        await using var scope = host;

        var response = await host.SendAsync(HttpMethod.Post, LookupUrl, new { number, email = BuyerEmail });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await JsonOf(response);
        Assert.Equal((number, "New", 3000), (
            order.GetProperty("number").GetInt32(),
            order.GetProperty("status").GetString(),
            order.GetProperty("total").GetInt32()));
        Assert.False(order.TryGetProperty("id", out _));
        Assert.False(order.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task Lookup_WithAWrongEmailAndWithAnUnknownNumber_AnswersTheSame404()
    {
        var (host, number, _) = await PlacedAsync();
        await using var scope = host;

        var wrongEmail = await host.SendAsync(HttpMethod.Post, LookupUrl, new { number, email = "someone@else.test" });
        var unknownNumber = await host.SendAsync(HttpMethod.Post, LookupUrl, new { number = number + 1, email = BuyerEmail });

        Assert.Equal(HttpStatusCode.NotFound, wrongEmail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownNumber.StatusCode);
        var first = await JsonOf(wrongEmail);
        var second = await JsonOf(unknownNumber);
        Assert.Equal("order.not_found", first.GetProperty("code").GetString());
        Assert.Equal(first.GetProperty("code").GetString(), second.GetProperty("code").GetString());
        Assert.Equal(first.GetProperty("detail").GetString(), second.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Lookup_WithAnEmptyEmail_Is400()
    {
        var (host, number, _) = await PlacedAsync();
        await using var scope = host;

        var response = await host.SendAsync(HttpMethod.Post, LookupUrl, new { number, email = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(LookupUrl)]
    [InlineData(TokenUrl)]
    public async Task TheLookups_WithoutTheCsrfHeader_AreRefused(string url)
    {
        var (host, number, token) = await PlacedAsync();
        await using var scope = host;

        var response = await host.SendAsync(HttpMethod.Post, url, new { number, email = BuyerEmail, token }, csrfHeader: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LookupByToken_WithTheKeyOfTheEmail_Answers200_AndAWrongKeyAnswers404()
    {
        var (host, number, token) = await PlacedAsync();
        await using var scope = host;

        var found = await host.SendAsync(HttpMethod.Post, TokenUrl, new { token });
        var wrong = await host.SendAsync(HttpMethod.Post, TokenUrl, new { token = token[..^1] + (token[^1] == 'A' ? 'B' : 'A') });

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(number, (await JsonOf(found)).GetProperty("number").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        Assert.Equal("order.not_found", (await JsonOf(wrong)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Lookups_AreLimitedPerMinute_AndTheAnswerIs429()
    {
        var (host, number, _) = await PlacedAsync(new Dictionary<string, string> { ["Checkout:LookupAttemptsPerMinute"] = "3" });
        await using var scope = host;

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            statuses.Add((await host.SendAsync(HttpMethod.Post, LookupUrl, new { number, email = BuyerEmail })).StatusCode);
        }

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests],
            statuses);
    }

    [Fact]
    public async Task PlacingOrders_IsLimitedPerCart_AndTheAnswerIs429()
    {
        await using var host = await AuthApiHost.CreateAsync(
            postgres, extraSettings: new Dictionary<string, string> { ["Checkout:PlaceAttemptsPerMinute"] = "2" });
        var cookie = await CartCookieWithAsync(host, await SeedVariantAsync(host));

        // The cart is emptied by the first order, so the next ones are refused for another reason; only the limit is in question.
        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            statuses.Add((await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(), cookie)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Created, statuses[0]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, statuses[1]);
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[2]);
    }

    [Fact]
    public async Task AnotherCart_IsNotSlowedDownByTheLimitOfThisOne()
    {
        await using var host = await AuthApiHost.CreateAsync(
            postgres, extraSettings: new Dictionary<string, string> { ["Checkout:PlaceAttemptsPerMinute"] = "1" });
        var variantId = await SeedVariantAsync(host);
        var first = await CartCookieWithAsync(host, variantId);
        var second = await CartCookieWithAsync(host, variantId);

        var one = await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(), first);
        var other = await host.SendAsync(HttpMethod.Post, OrdersUrl, Body(), second);

        Assert.Equal(HttpStatusCode.Created, one.StatusCode);
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
    }
}
