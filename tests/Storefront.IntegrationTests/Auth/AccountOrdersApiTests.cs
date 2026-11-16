using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storefront.Domain.Catalog;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

/// <summary>The buyer's account with orders and carts (task 30e): placing an order, the history, the guest orders and the joined cart.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AccountOrdersApiTests(PostgresFixture postgres)
{
    private const string Base = "/api/v1/account";
    private const string CustomerCookieName = "__Host-printko-customer";
    private const string CartCookieName = "__Host-printko-cart";
    private const string Email = "buyer@example.test";
    private const string Password = "a made-up buyer password";

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string? CookieNamed(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(name + "=", StringComparison.Ordinal))?.Split(';')[0]
            : null;

    private static string Both(string customer, string? cart) => cart is null ? customer : $"{customer}; {cart}";

    private static async Task<(Guid First, Guid Second)> SeedAsync(AuthApiHost host)
    {
        await using var context = host.Database.CreateContext();
        var category = CatalogData.Category();
        var first = CatalogData.Product(category.Id, "dragon");
        first.Status = ProductStatus.Published;
        var second = CatalogData.Product(category.Id, "castle");
        second.Status = ProductStatus.Published;
        var firstVariant = CatalogData.Variant(first, "DRG-001");
        var secondVariant = CatalogData.Variant(second, "CST-001");
        firstVariant.StockQuantity = 100;
        secondVariant.StockQuantity = 100;
        context.Add(category);
        context.Add(first);
        context.Add(second);
        context.Add(CatalogData.Settings());
        context.Add(CatalogData.FreeShipping());
        await context.SaveChangesAsync();
        return (firstVariant.Id, secondVariant.Id);
    }

    private static async Task<string> RegisterAndLoginAsync(
        AuthApiHost host, string email = Email, string? cartCookie = null, bool confirm = false)
    {
        await host.SendAsync(HttpMethod.Post, $"{Base}/register", new { email, password = Password, name = "Buyer" });
        if (confirm)
        {
            await using var context = host.Database.CreateContext();
            await context.CustomerUsers.Where(u => u.Email == email).ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailConfirmed, true));
        }

        var response = await host.SendAsync(HttpMethod.Post, $"{Base}/login", new { email, password = Password }, cartCookie);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return CookieNamed(response, CustomerCookieName)!;
    }

    private static async Task<HttpResponseMessage> LoginAsync(AuthApiHost host, string? cartCookie = null, string email = Email) =>
        await host.SendAsync(HttpMethod.Post, $"{Base}/login", new { email, password = Password }, cartCookie);

    private static async Task<string> AddToCartAsync(AuthApiHost host, Guid variantId, int quantity, string? cookie = null)
    {
        var response = await host.SendAsync(HttpMethod.Post, "/api/v1/cart/items", new { variantId, quantity }, cookie);
        Assert.True(response.IsSuccessStatusCode, $"Adding to the cart answered {(int)response.StatusCode}.");
        return CookieNamed(response, CartCookieName) ?? cookie!.Split(';').Single(p => p.Trim().StartsWith(CartCookieName, StringComparison.Ordinal)).Trim();
    }

    private static async Task<Dictionary<Guid, int>> CartLinesAsync(AuthApiHost host, string cookie)
    {
        var cart = await JsonOf(await host.SendAsync(HttpMethod.Get, "/api/v1/cart", cookie: cookie));
        return cart.GetProperty("lines").EnumerateArray()
            .ToDictionary(line => line.GetProperty("variantId").GetGuid(), line => line.GetProperty("quantity").GetInt32());
    }

    private static object OrderBody(string email = Email, long expectedTotal = 3000) => new
    {
        name = "Buyer",
        email,
        phone = "+359 88 123 4567",
        street = "12 Imaginary Street",
        city = "Sofia",
        postalCode = "1000",
        termsAccepted = true,
        shippingMethod = "address",
        expectedTotal,
    };

    private static async Task<int> PlaceOrderAsync(AuthApiHost host, Guid variantId, string? customerCookie, string email = Email, object? extra = null)
    {
        var cart = await AddToCartAsync(host, variantId, 2);
        var cookie = customerCookie is null ? cart : Both(customerCookie, cart);
        var body = extra is null ? OrderBody(email) : extra;
        var response = await host.SendAsync(HttpMethod.Post, "/api/v1/orders", body, cookie);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonOf(response)).GetProperty("number").GetInt32();
    }

    private static async Task<Guid?> OrderOwnerAsync(AuthApiHost host, int number)
    {
        await using var context = host.Database.CreateContext();
        return await context.Orders.AsNoTracking().Where(o => o.Number == number).Select(o => o.CustomerId).SingleAsync();
    }

    private static async Task<Guid> CustomerIdAsync(AuthApiHost host, string email = Email)
    {
        await using var context = host.Database.CreateContext();
        return await context.CustomerUsers.AsNoTracking().Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
    }

    // --- Placing an order ---

    [Fact]
    public async Task ASignedInBuyersOrder_IsOnTheirAccount_AndAGuestsIsNot()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var (variant, _) = await SeedAsync(host);
        var session = await RegisterAndLoginAsync(host);

        var mine = await PlaceOrderAsync(host, variant, session);
        var guest = await PlaceOrderAsync(host, variant, null);

        Assert.Equal(await CustomerIdAsync(host), await OrderOwnerAsync(host, mine));
        Assert.Null(await OrderOwnerAsync(host, guest));
    }

    [Fact]
    public async Task TheAccountOfAnOrder_NeverComesFromTheRequest()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var (variant, _) = await SeedAsync(host);
        await RegisterAndLoginAsync(host);
        var forged = new
        {
            name = "Buyer",
            email = "someone@example.test",
            phone = "+359 88 123 4567",
            street = "12 Imaginary Street",
            city = "Sofia",
            postalCode = "1000",
            termsAccepted = true,
            shippingMethod = "address",
            expectedTotal = 3000,
            customerId = await CustomerIdAsync(host),
        };

        var number = await PlaceOrderAsync(host, variant, null, extra: forged);

        Assert.Null(await OrderOwnerAsync(host, number));
    }

    // --- The history ---

    [Fact]
    public async Task Orders_ListsOnlyTheOrdersOfTheAccount_TheNewestFirst_WithPaging()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var (variant, _) = await SeedAsync(host);
        var session = await RegisterAndLoginAsync(host);
        var other = await RegisterAndLoginAsync(host, "other@example.test");
        var first = await PlaceOrderAsync(host, variant, session);
        var second = await PlaceOrderAsync(host, variant, session);
        await PlaceOrderAsync(host, variant, other, "other@example.test");

        var page = await JsonOf(await host.SendAsync(HttpMethod.Get, $"{Base}/orders?page=1&pageSize=1", cookie: session));
        var all = await JsonOf(await host.SendAsync(HttpMethod.Get, $"{Base}/orders", cookie: session));

        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(second, page.GetProperty("items")[0].GetProperty("number").GetInt32());
        Assert.Equal(
            [second, first],
            all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("number").GetInt32()).ToArray());
        var item = all.GetProperty("items")[0];
        Assert.Equal("New", item.GetProperty("status").GetString());
        Assert.Equal(3000, item.GetProperty("total").GetInt64());
        Assert.Equal(1, item.GetProperty("itemCount").GetInt32());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Orders_WithAnInvalidPage_Answers400(string query)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var session = await RegisterAndLoginAsync(host);

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}/orders?{query}", cookie: session);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Orders_WithoutASession_OrWithTheAdministratorsCookie_Answers401()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var anonymous = await host.SendAsync(HttpMethod.Get, $"{Base}/orders");
        var admin = await host.SendAsync(HttpMethod.Get, $"{Base}/orders", cookie: await host.LoginAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, admin.StatusCode);
    }

    [Fact]
    public async Task Order_OfTheAccount_IsShown_AndAnotherBuyersOrAnUnknownNumber_Answer404Alike()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var (variant, _) = await SeedAsync(host);
        var session = await RegisterAndLoginAsync(host);
        var other = await RegisterAndLoginAsync(host, "other@example.test");
        var mine = await PlaceOrderAsync(host, variant, session);
        var theirs = await PlaceOrderAsync(host, variant, other, "other@example.test");

        var shown = await host.SendAsync(HttpMethod.Get, $"{Base}/orders/{mine}", cookie: session);
        var foreign = await host.SendAsync(HttpMethod.Get, $"{Base}/orders/{theirs}", cookie: session);
        var unknown = await host.SendAsync(HttpMethod.Get, $"{Base}/orders/99999999", cookie: session);

        Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
        Assert.Equal(mine, (await JsonOf(shown)).GetProperty("number").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal((await JsonOf(foreign)).GetProperty("code").GetString(), (await JsonOf(unknown)).GetProperty("code").GetString());
    }

    // --- The orders placed as a guest ---

    [Fact]
    public async Task GuestOrders_JoinTheAccount_OnlyWhenTheAddressIsConfirmed()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var (variant, _) = await SeedAsync(host);
        var guest = await PlaceOrderAsync(host, variant, null, "Buyer@Example.test");
        var another = await PlaceOrderAsync(host, variant, null, "another@example.test");
        var session = await RegisterAndLoginAsync(host);

        Assert.Null(await OrderOwnerAsync(host, guest));

        await using (var context = host.Database.CreateContext())
        {
            await context.CustomerUsers.ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailConfirmed, true));
        }

        await LoginAsync(host);

        Assert.Equal(await CustomerIdAsync(host), await OrderOwnerAsync(host, guest));
        Assert.Null(await OrderOwnerAsync(host, another));
        var list = await JsonOf(await host.SendAsync(HttpMethod.Get, $"{Base}/orders", cookie: session));
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ConfirmingTheAddress_JoinsTheGuestOrdersToo()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var (variant, _) = await SeedAsync(host);
        var guest = await PlaceOrderAsync(host, variant, null);
        await RegisterAndLoginAsync(host);
        string token;
        await using (var context = host.Database.CreateContext())
        {
            var mail = await context.OutgoingEmails.AsNoTracking().Where(e => e.ToAddress == Email && e.Subject.StartsWith("Потвърдете")).SingleAsync();
            token = Uri.UnescapeDataString(System.Text.RegularExpressions.Regex.Match(mail.TextBody, @"token=([^\s]+)").Groups[1].Value);
        }

        var response = await host.SendAsync(HttpMethod.Post, $"{Base}/confirm-email", new { email = Email, token });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(await CustomerIdAsync(host), await OrderOwnerAsync(host, guest));
    }

    [Fact]
    public async Task AnOrderThatBelongsToAnAccount_IsNotTakenByAnotherWithTheSameEmailOfTheOrder()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var (variant, _) = await SeedAsync(host);
        var first = await RegisterAndLoginAsync(host, confirm: true);
        // The first buyer wrote another address as the contact of the order.
        var owned = await PlaceOrderAsync(host, variant, first, "other@example.test");
        var firstId = await CustomerIdAsync(host);

        await RegisterAndLoginAsync(host, "other@example.test", confirm: true);
        await LoginAsync(host, email: "other@example.test");

        Assert.Equal(firstId, await OrderOwnerAsync(host, owned));
    }

    [Fact]
    public async Task AnOrderWithoutAnOwner_JoinsANewAccountOfTheSameConfirmedAddress()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var (variant, _) = await SeedAsync(host);
        var first = await RegisterAndLoginAsync(host, confirm: true);
        var owned = await PlaceOrderAsync(host, variant, first);
        await using (var context = host.Database.CreateContext())
        {
            await context.CustomerUsers.ExecuteDeleteAsync();
        }

        Assert.Null(await OrderOwnerAsync(host, owned));
        await RegisterAndLoginAsync(host, confirm: true);

        Assert.Equal(await CustomerIdAsync(host), await OrderOwnerAsync(host, owned));
    }

    // --- The cart ---

    [Fact]
    public async Task SigningIn_GivesTheCartOfTheBrowserToTheAccount_AndKeepsItsCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var (variant, _) = await SeedAsync(host);
        var cart = await AddToCartAsync(host, variant, 3);
        await host.SendAsync(HttpMethod.Post, $"{Base}/register", new { email = Email, password = Password, name = "Buyer" });

        var response = await LoginAsync(host, cart);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(cart, CookieNamed(response, CartCookieName));
        await using var context = host.Database.CreateContext();
        Assert.Equal(await CustomerIdAsync(host), (await context.Carts.SingleAsync()).CustomerId);
    }

    [Fact]
    public async Task SigningOut_ForgetsTheCartCookie_AndTheNextSignInBringsTheCartBackWithANewCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var (variant, _) = await SeedAsync(host);
        var cart = await AddToCartAsync(host, variant, 3);
        var session = await RegisterAndLoginAsync(host, cartCookie: cart);

        var logout = await host.SendAsync(HttpMethod.Post, $"{Base}/logout", cookie: Both(session, cart));
        var back = await LoginAsync(host);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), v => v.StartsWith(CartCookieName + "=", StringComparison.Ordinal) && v.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        var newCart = CookieNamed(back, CartCookieName);
        Assert.NotNull(newCart);
        Assert.NotEqual(cart, newCart);
        Assert.Equal(3, (await CartLinesAsync(host, newCart))[variant]);
        // The old secret no longer opens the cart.
        Assert.Empty(await CartLinesAsync(host, cart));
    }

    [Fact]
    public async Task SigningIn_JoinsTheTwoCarts_AddsTheUnitsOfTheSameVariant_AndLeavesOneCartForTheAccount()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var (first, second) = await SeedAsync(host);
        var accountCart = await AddToCartAsync(host, first, 2);
        await RegisterAndLoginAsync(host, cartCookie: accountCart);
        await host.SendAsync(HttpMethod.Post, $"{Base}/logout", cookie: null);
        var browserCart = await AddToCartAsync(host, first, 3);
        await AddToCartAsync(host, second, 1, browserCart);

        var response = await LoginAsync(host, browserCart);

        var lines = await CartLinesAsync(host, CookieNamed(response, CartCookieName)!);
        Assert.Equal(5, lines[first]);
        Assert.Equal(1, lines[second]);
        await using var context = host.Database.CreateContext();
        var cartOfTheAccount = await context.Carts.AsNoTracking().SingleAsync();
        Assert.Equal(await CustomerIdAsync(host), cartOfTheAccount.CustomerId);
    }

    [Fact]
    public async Task JoiningTheCarts_NeverGoesOverTwentyUnitsOfOneVariant()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var (first, _) = await SeedAsync(host);
        var accountCart = await AddToCartAsync(host, first, 15);
        var session = await RegisterAndLoginAsync(host, cartCookie: accountCart);
        await host.SendAsync(HttpMethod.Post, $"{Base}/logout", cookie: Both(session, accountCart));
        var browserCart = await AddToCartAsync(host, first, 10);

        var response = await LoginAsync(host, browserCart);

        var lines = await CartLinesAsync(host, CookieNamed(response, CartCookieName)!);
        Assert.Equal(20, lines[first]);
        await using var context = host.Database.CreateContext();
        Assert.Equal(1, await context.Carts.CountAsync(c => c.CustomerId != null));
    }

    [Fact]
    public async Task ACartThatAnotherAccountLeftInTheBrowser_IsNotJoinedToThisBuyer()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var (first, _) = await SeedAsync(host);
        var leftBehind = await AddToCartAsync(host, first, 4);
        await RegisterAndLoginAsync(host, "other@example.test", cartCookie: leftBehind);
        await RegisterAndLoginAsync(host);

        // The browser still holds the cookie of the cart of the other account.
        var response = await LoginAsync(host, leftBehind);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(CookieNamed(response, CartCookieName));
        await using var context = host.Database.CreateContext();
        var other = await context.CustomerUsers.AsNoTracking().Where(u => u.Email == "other@example.test").Select(u => u.Id).SingleAsync();
        var cart = await context.Carts.AsNoTracking().Include(c => c.Items).SingleAsync();
        Assert.Equal(other, cart.CustomerId);
        Assert.Equal(4, cart.Items.Single().Quantity);
    }

    [Fact]
    public async Task SigningIn_WithNoCartAnywhere_IsNotAnError_AndSetsNoCartCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await host.SendAsync(HttpMethod.Post, $"{Base}/register", new { email = Email, password = Password, name = "Buyer" });

        var response = await LoginAsync(host);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(CookieNamed(response, CartCookieName));
    }
}
