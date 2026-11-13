using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storefront.Application.Carts;
using Storefront.Domain.Carts;
using Storefront.Domain.Catalog;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class CartApiTests(PostgresFixture postgres)
{
    private const string CartUrl = "/api/v1/cart";
    private const string ItemsUrl = "/api/v1/cart/items";
    private const string CookieName = "__Host-printko-cart";

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string? SetCookieHeaderOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal))
            : null;

    private static string? CookieOf(HttpResponseMessage response) => SetCookieHeaderOf(response)?.Split(';')[0];

    private static async Task<Guid> SeedVariantAsync(
        AuthApiHost host,
        string slug = "dragon",
        string sku = "DRG-001",
        ProductStatus status = ProductStatus.Published,
        int stock = 100,
        FulfilmentType fulfilmentType = FulfilmentType.InStock,
        int? leadTimeDays = null)
    {
        await using var context = host.Database.CreateContext();
        var category = await context.Categories.FirstOrDefaultAsync();
        if (category is null)
        {
            category = CatalogData.Category();
            context.Add(category);
        }

        var product = CatalogData.Product(category.Id, slug, fulfilmentType, leadTimeDays);
        product.Status = status;
        var variant = CatalogData.Variant(product, sku);
        variant.StockQuantity = stock;
        context.Add(product);
        await context.SaveChangesAsync();
        return variant.Id;
    }

    private static Task<HttpResponseMessage> AddAsync(AuthApiHost host, Guid variantId, int quantity, string? cookie = null) =>
        host.SendAsync(HttpMethod.Post, ItemsUrl, new { variantId, quantity }, cookie);

    [Fact]
    public async Task Get_WithoutACookie_IsAnEmptyCart_AndSetsNoCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(HttpMethod.Get, CartUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cart = await JsonOf(response);
        Assert.Equal(0, cart.GetProperty("lines").GetArrayLength());
        Assert.Equal((0, 0), (cart.GetProperty("itemCount").GetInt32(), cart.GetProperty("total").GetInt32()));
        Assert.Null(SetCookieHeaderOf(response));
        await using var context = host.Database.CreateContext();
        Assert.Empty(await context.Carts.ToListAsync());
    }

    [Fact]
    public async Task Get_WithAnUnknownOrBrokenCookie_IsAnEmptyCart_NotAnError()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var unknown = await host.SendAsync(HttpMethod.Get, CartUrl, cookie: $"{CookieName}={CartToken.Create()}");
        var broken = await host.SendAsync(HttpMethod.Get, CartUrl, cookie: $"{CookieName}=%%%not-a-token");

        await AssertEmptyCartAsync(unknown);
        await AssertEmptyCartAsync(broken);
    }

    private static async Task AssertEmptyCartAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await JsonOf(response)).GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task FirstAdd_CreatesTheCart_AndSetsAHardenedCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);

        var response = await AddAsync(host, variantId, 2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var line = (await JsonOf(response)).GetProperty("lines")[0];
        Assert.Equal(variantId, line.GetProperty("variantId").GetGuid());
        Assert.Equal(2, line.GetProperty("quantity").GetInt32());
        Assert.Equal("Available", line.GetProperty("state").GetString());
        var header = SetCookieHeaderOf(response)!.ToLowerInvariant();
        Assert.Contains("httponly", header, StringComparison.Ordinal);
        Assert.Contains("secure", header, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", header, StringComparison.Ordinal);
        Assert.Contains("path=/", header, StringComparison.Ordinal);
        Assert.Contains("max-age=2592000", header, StringComparison.Ordinal);
        Assert.DoesNotContain("domain=", header, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCookie_GivesBackTheCart_AndEveryChangeRenewsIt()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var cookie = CookieOf(await AddAsync(host, variantId, 2))!;

        var read = await JsonOf(await host.SendAsync(HttpMethod.Get, CartUrl, cookie: cookie));
        var again = await AddAsync(host, variantId, 3, cookie);

        Assert.Equal(2, read.GetProperty("lines")[0].GetProperty("quantity").GetInt32());
        Assert.Equal(5, (await JsonOf(again)).GetProperty("lines")[0].GetProperty("quantity").GetInt32());
        Assert.Equal(cookie, CookieOf(again));
    }

    [Fact]
    public async Task AddWithAnUnknownCookie_StartsANewCart_WithANewCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var unknown = $"{CookieName}={CartToken.Create()}";

        var response = await AddAsync(host, variantId, 1, unknown);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(unknown, CookieOf(response));
        await using var context = host.Database.CreateContext();
        Assert.Single(await context.Carts.ToListAsync());
    }

    [Fact]
    public async Task SetQuantity_RemoveAndClear_ChangeTheCart_AndKeepTheCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var first = await SeedVariantAsync(host);
        var second = await SeedVariantAsync(host, "phoenix", "PHX-001");
        var cookie = CookieOf(await AddAsync(host, first, 1))!;
        await AddAsync(host, second, 1, cookie);

        var set = await host.SendAsync(HttpMethod.Put, $"{ItemsUrl}/{first}", new { quantity = 9 }, cookie);
        var remove = await host.SendAsync(HttpMethod.Delete, $"{ItemsUrl}/{second}", cookie: cookie);
        var afterRemove = await JsonOf(await host.SendAsync(HttpMethod.Get, CartUrl, cookie: cookie));
        var clear = await host.SendAsync(HttpMethod.Delete, CartUrl, cookie: cookie);
        var afterClear = await JsonOf(await host.SendAsync(HttpMethod.Get, CartUrl, cookie: cookie));

        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Equal(9, (await JsonOf(set)).GetProperty("lines")[0].GetProperty("quantity").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);
        Assert.Equal(1, afterRemove.GetProperty("lines").GetArrayLength());
        Assert.Equal(0, afterClear.GetProperty("lines").GetArrayLength());
        Assert.All([set, remove, clear], response => Assert.Equal(cookie, CookieOf(response)));
    }

    [Fact]
    public async Task Clear_WithoutACart_Is204_WithNoCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(HttpMethod.Delete, CartUrl);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(SetCookieHeaderOf(response));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task ChangesWithoutTheCsrfHeader_AreRefused_AndChangeNothing(string method)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var cookie = CookieOf(await AddAsync(host, variantId, 2))!;
        var url = method == "POST" ? ItemsUrl : $"{ItemsUrl}/{variantId}";

        var response = await host.SendAsync(
            new HttpMethod(method), url, new { variantId, quantity = 9 }, cookie, csrfHeader: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.IsProblemJson());
        Assert.Equal("csrf.header_required", (await JsonOf(response)).GetProperty("code").GetString());
        await using var context = host.Database.CreateContext();
        Assert.Equal(2, (await context.CartItems.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task AddOfANewLine_Is201_WithTheAddressOfTheLine_AndTheCartInTheBody()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);

        var response = await AddAsync(host, variantId, 2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"/api/v1/cart/items/{variantId}", response.Headers.Location!.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(2, (await JsonOf(response)).GetProperty("itemCount").GetInt32());
    }

    [Fact]
    public async Task AddToALineThatIsAlreadyThere_Is200_WithNoLocation()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var cookie = CookieOf(await AddAsync(host, variantId, 1))!;

        var again = await AddAsync(host, variantId, 2, cookie);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Null(again.Headers.Location);
        Assert.Equal(3, (await JsonOf(again)).GetProperty("lines")[0].GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task TheAddressOfANewLine_ReturnsThatLine()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var created = await AddAsync(host, variantId, 4);
        var cookie = CookieOf(created)!;

        var line = await host.SendAsync(HttpMethod.Get, created.Headers.Location!.PathAndQuery, cookie: cookie);

        Assert.Equal(HttpStatusCode.OK, line.StatusCode);
        var body = await JsonOf(line);
        Assert.Equal(variantId, body.GetProperty("variantId").GetGuid());
        Assert.Equal(4, body.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task GetLine_ThatIsNotThere_OrOfAnotherShopper_Is404()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var cookie = CookieOf(await AddAsync(host, variantId, 1))!;
        var stranger = $"{CookieName}={CartToken.Create()}";

        var missing = await host.SendAsync(HttpMethod.Get, $"{ItemsUrl}/{Guid.NewGuid()}", cookie: cookie);
        var noCookie = await host.SendAsync(HttpMethod.Get, $"{ItemsUrl}/{variantId}");
        var other = await host.SendAsync(HttpMethod.Get, $"{ItemsUrl}/{variantId}", cookie: stranger);

        Assert.All([missing, noCookie, other], response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
    }

    [Fact]
    public async Task Add_TwentyUnits_IsAccepted()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);

        var response = await AddAsync(host, variantId, 20);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(20, (await JsonOf(response)).GetProperty("lines")[0].GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task Add_TwentyOneUnits_Is422_WithTheCode_AndSetsNoCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);

        var response = await AddAsync(host, variantId, 21);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.True(response.IsProblemJson());
        Assert.Equal("cart.quantity_too_high", (await JsonOf(response)).GetProperty("code").GetString());
        Assert.Null(SetCookieHeaderOf(response));
    }

    [Fact]
    public async Task Add_MoreThanIsInStock_Is422_WithTheCode_AndSetsNoCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host, stock: 2);

        var response = await AddAsync(host, variantId, 3);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.True(response.IsProblemJson());
        Assert.Equal("cart.quantity_exceeds_stock", (await JsonOf(response)).GetProperty("code").GetString());
        Assert.Null(SetCookieHeaderOf(response));
    }

    [Fact]
    public async Task SetQuantity_AboveTheStock_Is422_WithTheCode()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host, stock: 2);
        var cookie = CookieOf(await AddAsync(host, variantId, 1))!;

        var response = await host.SendAsync(
            HttpMethod.Put, $"{ItemsUrl}/{variantId}", new { quantity = 3 }, cookie);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("cart.quantity_exceeds_stock", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task TheLine_CarriesTheFulfilmentTheLeadTimeAndTheLimitOfTheStepper()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var stocked = await SeedVariantAsync(host, stock: 4);
        var madeToOrder = await SeedVariantAsync(
            host, "vase", "VAS-001", fulfilmentType: FulfilmentType.MadeToOrder, leadTimeDays: 7);
        var cookie = CookieOf(await AddAsync(host, stocked, 1))!;
        await AddAsync(host, madeToOrder, 1, cookie);

        var lines = (await JsonOf(await host.SendAsync(HttpMethod.Get, CartUrl, cookie: cookie))).GetProperty("lines");

        var first = lines[0];
        Assert.Equal("InStock", first.GetProperty("fulfilmentType").GetString());
        Assert.Equal(4, first.GetProperty("availableQuantity").GetInt32());
        var second = lines[1];
        Assert.Equal("MadeToOrder", second.GetProperty("fulfilmentType").GetString());
        Assert.Equal(7, second.GetProperty("leadTimeDays").GetInt32());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("availableQuantity").ValueKind);
    }

    [Fact]
    public async Task Add_ToACartWithFiftyItems_IsRefused_WithTheTooManyItemsCode()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var extra = await SeedVariantAsync(host, "extra", "EXTRA-1");
        var token = CartToken.Create();
        CartToken.TryHash(token, out var hash);
        await using (var context = host.Database.CreateContext())
        {
            var category = await context.Categories.FirstAsync();
            var cart = new Cart { TokenHash = hash };
            for (var i = 0; i < Cart.MaxItems; i++)
            {
                var product = CatalogData.Product(category.Id, $"p{i}");
                product.Status = ProductStatus.Published;
                var variant = CatalogData.Variant(product, $"SKU-{i}");
                context.Add(product);
                cart.Items.Add(new CartItem { CartId = cart.Id, VariantId = variant.Id, Quantity = 1 });
            }

            context.Add(cart);
            await context.SaveChangesAsync();
        }

        var response = await AddAsync(host, extra, 1, $"{CookieName}={token}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("cart.too_many_items", (await JsonOf(response)).GetProperty("code").GetString());
        await using var after = host.Database.CreateContext();
        Assert.Equal(Cart.MaxItems, await after.CartItems.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Add_AQuantityBelowOne_Is400_WithTheFieldNamed(int quantity)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);

        var response = await AddAsync(host, variantId, quantity);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await JsonOf(response);
        Assert.Equal("validation.failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("quantity", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Add_AVariantThatCannotBeOrdered_Is404_AndTheSameAsAnUnknownOne(bool isDraft)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = isDraft
            ? await SeedVariantAsync(host, status: ProductStatus.Draft)
            : Guid.NewGuid();

        var response = await AddAsync(host, variantId, 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("variant.not_found", (await JsonOf(response)).GetProperty("code").GetString());
        Assert.Null(SetCookieHeaderOf(response));
    }

    [Fact]
    public async Task SetQuantityAndRemove_OfALineThatIsNotThere_Are404()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var cookie = CookieOf(await AddAsync(host, variantId, 1))!;

        var set = await host.SendAsync(HttpMethod.Put, $"{ItemsUrl}/{Guid.NewGuid()}", new { quantity = 2 }, cookie);
        var remove = await host.SendAsync(HttpMethod.Delete, $"{ItemsUrl}/{Guid.NewGuid()}", cookie: cookie);
        var noCookie = await host.SendAsync(HttpMethod.Delete, $"{ItemsUrl}/{variantId}");

        await AssertLineNotFoundAsync(set);
        await AssertLineNotFoundAsync(remove);
        await AssertLineNotFoundAsync(noCookie);
    }

    private static async Task AssertLineNotFoundAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("cart.item_not_found", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ACartCannotBeReadOrChangedWithAnotherShoppersCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        await AddAsync(host, variantId, 1);
        var stranger = $"{CookieName}={CartToken.Create()}";

        var read = await JsonOf(await host.SendAsync(HttpMethod.Get, CartUrl, cookie: stranger));
        var remove = await host.SendAsync(HttpMethod.Delete, $"{ItemsUrl}/{variantId}", cookie: stranger);

        Assert.Equal(0, read.GetProperty("lines").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
        await using var context = host.Database.CreateContext();
        Assert.Single(await context.CartItems.ToListAsync());
    }

    [Fact]
    public async Task TheCartAddressesAreOpen_ButTheCartCookieIsNotTheAdminSession()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var variantId = await SeedVariantAsync(host);
        var cookie = CookieOf(await AddAsync(host, variantId, 1))!;

        var admin = await host.SendAsync(HttpMethod.Get, "/api/v1/admin/products", cookie: cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, admin.StatusCode);
    }
}
