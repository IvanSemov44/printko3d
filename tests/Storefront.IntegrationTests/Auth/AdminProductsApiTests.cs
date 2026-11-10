using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storefront.Domain.Catalog;
using Storefront.IntegrationTests.Persistence;
using Storefront.IntegrationTests.ReadApi;

namespace Storefront.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class AdminProductsApiTests(PostgresFixture postgres)
{
    private const string Base = "/api/v1/admin/products";
    private static readonly string Id = "00000000-0000-0000-0000-000000000001";

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string CodeOf(JsonElement problem) => problem.GetProperty("code").GetString()!;

    private static async Task SeedAsync(AuthApiHost host, Action<Storefront.Infrastructure.Persistence.StorefrontDbContext> add)
    {
        await using var context = host.Database.CreateContext();
        add(context);
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> SeedCategoryAsync(AuthApiHost host, string slug = "figures", bool isActive = true)
    {
        await using var context = host.Database.CreateContext();
        var category = ReadApiData.Category(slug, isActive: isActive);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    private static object NewProduct(
        Guid categoryId,
        string slug = "dragon",
        string? name = null,
        string type = "InStock",
        int? leadTimeDays = null) =>
        new
        {
            categoryId,
            name = name ?? slug,
            slug,
            shortDescription = "Short",
            description = "Long",
            fulfilmentType = type,
            leadTimeDays,
            seoTitle = (string?)null,
            seoDescription = (string?)null,
        };

    private static async Task<JsonElement> CreateProductAsync(
        AuthApiHost host, string cookie, Guid categoryId, string slug = "dragon", string type = "InStock", int? leadTimeDays = null)
    {
        var response = await host.SendAsync(HttpMethod.Post, Base, NewProduct(categoryId, slug, null, type, leadTimeDays), cookie);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await JsonOf(response);
    }

    private static object NewVariant(string sku, long price = 1000, int? stock = 5, bool isActive = true, int sortOrder = 0) =>
        new
        {
            sku,
            name = sku,
            price,
            weightGrams = 100,
            compareAtPrice = (long?)null,
            stockQuantity = stock,
            attributes = new Dictionary<string, string> { ["color"] = "blue" },
            isActive,
            sortOrder,
        };

    private static async Task<JsonElement> AddVariantAsync(
        AuthApiHost host, string cookie, Guid productId, string sku, long price = 1000, int? stock = 5, bool isActive = true)
    {
        var response = await host.SendAsync(
            HttpMethod.Post, $"{Base}/{productId}/variants", NewVariant(sku, price, stock, isActive), cookie);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await JsonOf(response);
    }

    private static Task<HttpResponseMessage> SetStatusAsync(
        AuthApiHost host, string cookie, JsonElement product, string status) =>
        host.SendAsync(
            HttpMethod.Put,
            $"{Base}/{product.GetProperty("id").GetGuid()}/status",
            new { status, version = product.GetProperty("version").GetUInt32() },
            cookie);

    private static object Update(JsonElement product, string? name = null, string? slug = null) =>
        new
        {
            categoryId = product.GetProperty("categoryId").GetGuid(),
            name = name ?? product.GetProperty("name").GetString(),
            slug = slug ?? product.GetProperty("slug").GetString(),
            shortDescription = product.GetProperty("shortDescription").GetString(),
            description = product.GetProperty("description").GetString(),
            fulfilmentType = product.GetProperty("fulfilmentType").GetString(),
            leadTimeDays = product.GetProperty("leadTimeDays").ValueKind == JsonValueKind.Null
                ? (int?)null
                : product.GetProperty("leadTimeDays").GetInt32(),
            seoTitle = (string?)null,
            seoDescription = (string?)null,
            version = product.GetProperty("version").GetUInt32(),
        };

    private static async Task<JsonElement> GetProductAsync(AuthApiHost host, string cookie, Guid id) =>
        await JsonOf(await host.SendAsync(HttpMethod.Get, $"{Base}/{id}", cookie: cookie));

    private static async Task<string[]> PublicSlugsAsync(AuthApiHost host)
    {
        var body = await JsonOf(await host.SendAsync(HttpMethod.Get, "/api/v1/products?pageSize=48"));
        return body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("slug").GetString()!).Order().ToArray();
    }

    private static async Task<HttpStatusCode> PublicProductStatusAsync(AuthApiHost host, string slug) =>
        (await host.SendAsync(HttpMethod.Get, $"/api/v1/products/{slug}")).StatusCode;

    // --- Who may call ---

    public static TheoryData<string, string> Addresses => new()
    {
        { "GET", Base },
        { "GET", $"{Base}/{Id}" },
        { "POST", Base },
        { "PUT", $"{Base}/{Id}" },
        { "PUT", $"{Base}/{Id}/status" },
        { "POST", $"{Base}/{Id}/variants" },
        { "PUT", $"{Base}/{Id}/variants/{Id}" },
        { "PUT", $"{Base}/{Id}/variants/{Id}/active" },
        { "POST", $"{Base}/{Id}/variants/{Id}/move" },
    };

    [Theory]
    [MemberData(nameof(Addresses))]
    public async Task EveryAdminAddress_Answers401WithoutASignIn_AsProblemDetails(string method, string url)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(new HttpMethod(method), url, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Theory]
    [MemberData(nameof(Addresses))]
    public async Task EveryAdminAddress_Answers403WithoutTheAdminRole_AsProblemDetails(string method, string url)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await host.CreateUserWithoutRoleAsync("viewer@example.test", "another made-up password");
        var cookie = await host.LoginAsync("viewer@example.test", "another made-up password");

        var response = await host.SendAsync(new HttpMethod(method), url, new { }, cookie);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    // --- Create, read, update ---

    [Fact]
    public async Task Create_ReturnsADraftDto_WithTheRowVersion_AndTheListShowsIt()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var categoryId = await SeedCategoryAsync(host);

        var response = await host.SendAsync(HttpMethod.Post, Base, NewProduct(categoryId), cookie);
        var created = await JsonOf(response);
        var list = await JsonOf(await host.SendAsync(HttpMethod.Get, Base, cookie: cookie));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"{Base}/{created.GetProperty("id").GetGuid()}", response.Headers.Location!.AbsolutePath);
        Assert.Equal("Draft", created.GetProperty("status").GetString());
        Assert.Equal("InStock", created.GetProperty("fulfilmentType").GetString());
        Assert.Empty(created.GetProperty("variants").EnumerateArray());
        Assert.True(created.GetProperty("version").GetUInt32() > 0);
        var row = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(created.GetProperty("id").GetGuid(), row.GetProperty("id").GetGuid());
        Assert.Equal("figures", row.GetProperty("categoryName").GetString());
    }

    [Fact]
    public async Task Get_OfAnUnknownProduct_Returns404WithACode()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}/{Id}", cookie: cookie);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.IsProblemJson());
        Assert.Equal("product.not_found", CodeOf(await JsonOf(response)));
    }

    [Fact]
    public async Task Update_ChangesTheProduct_AndReturnsTheNewVersion()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));

        var response = await host.SendAsync(
            HttpMethod.Put, $"{Base}/{created.GetProperty("id").GetGuid()}", Update(created, name: "Дракон"), cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await JsonOf(response);
        Assert.Equal("Дракон", updated.GetProperty("name").GetString());
        Assert.NotEqual(created.GetProperty("version").GetUInt32(), updated.GetProperty("version").GetUInt32());
    }

    [Fact]
    public async Task Update_OfAnUnknownProduct_Returns404()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));

        var response = await host.SendAsync(HttpMethod.Put, $"{Base}/{Id}", Update(created), cookie);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("product.not_found", CodeOf(await JsonOf(response)));
    }

    // --- Input and rule errors ---

    [Theory]
    [InlineData("", "Dragon", "slug")]
    [InlineData("Bad Slug", "Dragon", "slug")]
    [InlineData("dragon", "", "name")]
    public async Task Create_WithInvalidInput_Returns400WithTheFieldInTheErrors(string slug, string name, string field)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var categoryId = await SeedCategoryAsync(host);

        var response = await host.SendAsync(HttpMethod.Post, Base, NewProduct(categoryId, slug, name), cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
        var body = await JsonOf(response);
        Assert.Equal("validation.failed", CodeOf(body));
        Assert.Contains(
            body.GetProperty("errors").EnumerateObject(),
            e => string.Equals(e.Name, field, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_MadeToOrderWithoutALeadTime_Returns400OnTheLeadTimeField()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var categoryId = await SeedCategoryAsync(host);

        var response = await host.SendAsync(
            HttpMethod.Post, Base, NewProduct(categoryId, type: "MadeToOrder", leadTimeDays: 0), cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            (await JsonOf(response)).GetProperty("errors").EnumerateObject(),
            e => string.Equals(e.Name, "leadTimeDays", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_WithAnUnknownFulfilmentType_Returns400()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var categoryId = await SeedCategoryAsync(host);

        var response = await host.SendAsync(HttpMethod.Post, Base, NewProduct(categoryId, type: "Dropship"), cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_InAnUnknownCategory_Returns404WithACode()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Post, Base, NewProduct(Guid.CreateVersion7()), cookie);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("product.category_not_found", CodeOf(await JsonOf(response)));
    }

    [Fact]
    public async Task Create_InAnInactiveCategory_IsAccepted()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var categoryId = await SeedCategoryAsync(host, isActive: false);

        var response = await host.SendAsync(HttpMethod.Post, Base, NewProduct(categoryId), cookie);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithATakenSlug_Returns409WithACode()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var categoryId = await SeedCategoryAsync(host);
        await CreateProductAsync(host, cookie, categoryId, "dragon");

        var response = await host.SendAsync(HttpMethod.Post, Base, NewProduct(categoryId, "dragon", "Other"), cookie);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(response.IsProblemJson());
        Assert.Equal("product.slug_taken", CodeOf(await JsonOf(response)));
    }

    [Fact]
    public async Task ManyCreatesOfTheSameSlugAtOnce_GiveExactlyOneProduct_AndNeverA500()
    {
        // Two requests can both pass the "is the slug free" check; the unique index in the database must then decide.
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var categoryId = await SeedCategoryAsync(host);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            host.SendAsync(HttpMethod.Post, Base, NewProduct(categoryId, "dragon", $"Dragon {i}"), cookie)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var context = host.Database.CreateContext();
        Assert.Equal(1, await context.Products.CountAsync());
    }

    [Fact]
    public async Task Update_ToTheSlugOfAnotherProduct_Returns409()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var categoryId = await SeedCategoryAsync(host);
        await CreateProductAsync(host, cookie, categoryId, "dragon");
        var phoenix = await CreateProductAsync(host, cookie, categoryId, "phoenix");

        var response = await host.SendAsync(
            HttpMethod.Put, $"{Base}/{phoenix.GetProperty("id").GetGuid()}", Update(phoenix, slug: "dragon"), cookie);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("product.slug_taken", CodeOf(await JsonOf(response)));
    }

    // --- Two people editing at once ---

    [Fact]
    public async Task Update_WithAStaleVersion_Returns409WithACode_AndKeepsTheOtherEdit()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));
        var url = $"{Base}/{created.GetProperty("id").GetGuid()}";

        var first = await host.SendAsync(HttpMethod.Put, url, Update(created, name: "First"), cookie);
        var second = await host.SendAsync(HttpMethod.Put, url, Update(created, name: "Second"), cookie);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("product.version_conflict", CodeOf(await JsonOf(second)));
        var current = await JsonOf(await host.SendAsync(HttpMethod.Get, url, cookie: cookie));
        Assert.Equal("First", current.GetProperty("name").GetString());
    }

    [Fact]
    public async Task TwoUpdatesWithTheSameVersionAtOnce_GiveOneSuccessAndOneConflict()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));
        var url = $"{Base}/{created.GetProperty("id").GetGuid()}";

        var responses = await Task.WhenAll(
            host.SendAsync(HttpMethod.Put, url, Update(created, name: "A"), cookie),
            host.SendAsync(HttpMethod.Put, url, Update(created, name: "B"), cookie));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task ChangingTheStatus_WithAStaleVersion_Returns409_AndLeavesTheStatus()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));
        var id = created.GetProperty("id").GetGuid();
        await AddVariantAsync(host, cookie, id, "DRG-1");
        await host.SendAsync(HttpMethod.Put, $"{Base}/{id}", Update(created, name: "Changed"), cookie);

        var response = await SetStatusAsync(host, cookie, created, "Published");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("product.version_conflict", CodeOf(await JsonOf(response)));
        Assert.Equal("Draft", (await GetProductAsync(host, cookie, id)).GetProperty("status").GetString());
    }

    // --- Variants ---

    [Fact]
    public async Task AddVariant_ReturnsTheProductWithItsVariants_AndKeepsTheProductVersion()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));

        var product = await AddVariantAsync(host, cookie, created.GetProperty("id").GetGuid(), "DRG-1", price: 1250);

        var variant = Assert.Single(product.GetProperty("variants").EnumerateArray());
        Assert.Equal("DRG-1", variant.GetProperty("sku").GetString());
        Assert.Equal(1250, variant.GetProperty("price").GetInt64());
        Assert.Equal("blue", variant.GetProperty("attributes").GetProperty("color").GetString());
        Assert.True(variant.GetProperty("isActive").GetBoolean());
        Assert.Equal(created.GetProperty("version").GetUInt32(), product.GetProperty("version").GetUInt32());
    }

    [Fact]
    public async Task AddVariant_WithATakenSku_Returns409WithACode_EvenOnAnotherProduct()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var categoryId = await SeedCategoryAsync(host);
        var first = await CreateProductAsync(host, cookie, categoryId, "dragon");
        var second = await CreateProductAsync(host, cookie, categoryId, "phoenix");
        await AddVariantAsync(host, cookie, first.GetProperty("id").GetGuid(), "SKU-1");

        var response = await host.SendAsync(
            HttpMethod.Post, $"{Base}/{second.GetProperty("id").GetGuid()}/variants", NewVariant("SKU-1"), cookie);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("variant.sku_taken", CodeOf(await JsonOf(response)));
    }

    [Fact]
    public async Task ManyVariantsWithTheSameSkuAtOnce_GiveExactlyOne_AndNeverA500()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var categoryId = await SeedCategoryAsync(host);
        var ids = new List<Guid>();
        for (var i = 0; i < 8; i++)
        {
            ids.Add((await CreateProductAsync(host, cookie, categoryId, $"dragon-{i}")).GetProperty("id").GetGuid());
        }

        var responses = await Task.WhenAll(ids.Select(id =>
            host.SendAsync(HttpMethod.Post, $"{Base}/{id}/variants", NewVariant("SKU-1"), cookie)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var context = host.Database.CreateContext();
        Assert.Equal(1, await context.Set<ProductVariant>().CountAsync());
    }

    [Theory]
    [InlineData(-1, 0, "price")]
    [InlineData(100, 99, "compareAtPrice")]
    public async Task AddVariant_WithInvalidPrices_Returns400WithTheField(long price, long compareAt, string field)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));
        var body = new
        {
            sku = "A",
            name = "A",
            price,
            weightGrams = 10,
            compareAtPrice = compareAt,
            stockQuantity = 1,
            attributes = new Dictionary<string, string>(),
            isActive = true,
            sortOrder = 0,
        };

        var response = await host.SendAsync(
            HttpMethod.Post, $"{Base}/{created.GetProperty("id").GetGuid()}/variants", body, cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            (await JsonOf(response)).GetProperty("errors").EnumerateObject(),
            e => string.Equals(e.Name, field, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateVariant_ChangesThePrice_AndOfAnUnknownVariantReturns404()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));
        var id = created.GetProperty("id").GetGuid();
        var product = await AddVariantAsync(host, cookie, id, "DRG-1", price: 1000);
        var variantId = product.GetProperty("variants")[0].GetProperty("id").GetGuid();
        var edit = new
        {
            sku = "DRG-1",
            name = "Red",
            price = 1500L,
            weightGrams = 100,
            compareAtPrice = 2000L,
            stockQuantity = 3,
            attributes = new Dictionary<string, string>(),
        };

        var ok = await host.SendAsync(HttpMethod.Put, $"{Base}/{id}/variants/{variantId}", edit, cookie);
        var missing = await host.SendAsync(HttpMethod.Put, $"{Base}/{id}/variants/{Id}", edit, cookie);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var variant = (await JsonOf(ok)).GetProperty("variants")[0];
        Assert.Equal(1500, variant.GetProperty("price").GetInt64());
        Assert.Equal(2000, variant.GetProperty("compareAtPrice").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("variant.not_found", CodeOf(await JsonOf(missing)));
    }

    [Fact]
    public async Task TheLastActiveVariantOfAPublishedProduct_CannotBeDeactivated()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));
        var id = created.GetProperty("id").GetGuid();
        var product = await AddVariantAsync(host, cookie, id, "A");
        await SetStatusAsync(host, cookie, product, "Published");
        var variantId = product.GetProperty("variants")[0].GetProperty("id").GetGuid();

        var response = await host.SendAsync(
            HttpMethod.Put, $"{Base}/{id}/variants/{variantId}/active", new { isActive = false }, cookie);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("variant.last_active", CodeOf(await JsonOf(response)));
        Assert.True((await GetProductAsync(host, cookie, id)).GetProperty("variants")[0].GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Move_ChangesTheOrderOfTheVariants()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));
        var id = created.GetProperty("id").GetGuid();
        foreach (var (sku, order) in new[] { ("A", 0), ("B", 1), ("C", 2) })
        {
            var added = await host.SendAsync(HttpMethod.Post, $"{Base}/{id}/variants", NewVariant(sku, sortOrder: order), cookie);
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }

        var current = await GetProductAsync(host, cookie, id);
        var b = current.GetProperty("variants").EnumerateArray().Single(v => v.GetProperty("sku").GetString() == "B");

        var response = await host.SendAsync(
            HttpMethod.Post, $"{Base}/{id}/variants/{b.GetProperty("id").GetGuid()}/move", new { direction = "Up" }, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order2 = (await JsonOf(response)).GetProperty("variants").EnumerateArray()
            .Select(v => v.GetProperty("sku").GetString()!).ToArray();
        Assert.Equal(["B", "A", "C"], order2);
    }

    [Fact]
    public async Task Move_WithAnUnknownDirection_Returns400()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));
        var id = created.GetProperty("id").GetGuid();
        var product = await AddVariantAsync(host, cookie, id, "A");

        var response = await host.SendAsync(
            HttpMethod.Post,
            $"{Base}/{id}/variants/{product.GetProperty("variants")[0].GetProperty("id").GetGuid()}/move",
            new { direction = "Sideways" },
            cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheDetail_ContainsTheInactiveVariants_TooAndInOrder()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));
        var id = created.GetProperty("id").GetGuid();
        await host.SendAsync(HttpMethod.Post, $"{Base}/{id}/variants", NewVariant("A", sortOrder: 1), cookie);
        await host.SendAsync(HttpMethod.Post, $"{Base}/{id}/variants", NewVariant("B", sortOrder: 0, isActive: false), cookie);

        var variants = (await GetProductAsync(host, cookie, id)).GetProperty("variants").EnumerateArray().ToList();

        Assert.Equal(["B", "A"], variants.Select(v => v.GetProperty("sku").GetString()!));
        Assert.False(variants[0].GetProperty("isActive").GetBoolean());
    }

    // --- Status ---

    [Fact]
    public async Task Publishing_WithoutAnActiveVariant_Returns409WithACode()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));

        var response = await SetStatusAsync(host, cookie, created, "Published");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("product.cannot_publish_without_active_variant", CodeOf(await JsonOf(response)));
    }

    [Fact]
    public async Task ChangingTheStatus_ToAnUnknownValue_Returns400()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateProductAsync(host, cookie, await SeedCategoryAsync(host));

        var response = await SetStatusAsync(host, cookie, created, "Deleted");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- The list ---

    [Fact]
    public async Task List_ShowsEveryStatus_WithVariantCountLowestPriceAndStockOfTheActiveVariants()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var category = ReadApiData.Category("figures");
        var draft = ReadApiData.Product(category, "draft-product", status: ProductStatus.Draft, price: 500, stock: 2);
        ReadApiData.Variant(draft, "draft-cheap-inactive", price: 100, stock: 50, isActive: false);
        ReadApiData.Variant(draft, "draft-second", price: 800, stock: 4);
        var archived = ReadApiData.Product(category, "archived-product", status: ProductStatus.Archived);
        var made = ReadApiData.Product(category, "made-product", fulfilmentType: FulfilmentType.MadeToOrder, stock: null);
        await SeedAsync(host, context => ReadApiData.AddAll(context, [category], [draft, archived, made]));

        var list = await JsonOf(await host.SendAsync(HttpMethod.Get, $"{Base}?pageSize=100", cookie: cookie));

        Assert.Equal(3, list.GetProperty("totalCount").GetInt32());
        var rows = list.GetProperty("items").EnumerateArray().ToDictionary(r => r.GetProperty("slug").GetString()!);
        Assert.Equal("Draft", rows["draft-product"].GetProperty("status").GetString());
        Assert.Equal("Archived", rows["archived-product"].GetProperty("status").GetString());
        Assert.Equal(3, rows["draft-product"].GetProperty("variantCount").GetInt32());
        Assert.Equal(500, rows["draft-product"].GetProperty("minPrice").GetInt64());
        Assert.Equal(6, rows["draft-product"].GetProperty("totalStock").GetInt32());
        Assert.Equal(JsonValueKind.Null, rows["made-product"].GetProperty("totalStock").ValueKind);
        Assert.True(rows["draft-product"].GetProperty("version").GetUInt32() > 0);
    }

    [Fact]
    public async Task List_FiltersByStatusCategoryAndName_AndPages()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var figures = ReadApiData.Category("figures");
        var toys = ReadApiData.Category("toys");
        await SeedAsync(host, context => ReadApiData.AddAll(
            context,
            [figures, toys],
            [
                ReadApiData.Product(figures, "dragon", "Dragon"),
                ReadApiData.Product(figures, "dragonfly", "Dragonfly", status: ProductStatus.Draft),
                ReadApiData.Product(toys, "robot", "Robot"),
            ]));

        async Task<string[]> Slugs(string query) =>
            (await JsonOf(await host.SendAsync(HttpMethod.Get, $"{Base}?{query}", cookie: cookie)))
            .GetProperty("items").EnumerateArray().Select(i => i.GetProperty("slug").GetString()!).Order().ToArray();

        Assert.Equal(["dragonfly"], await Slugs("status=Draft"));
        Assert.Equal(["dragon", "dragonfly"], await Slugs($"categoryId={figures.Id}"));
        Assert.Equal(["dragon", "dragonfly"], await Slugs("search=DRAGON"));
        Assert.Equal(["dragon"], await Slugs($"search=drag&status=Published&categoryId={figures.Id}"));
        var paged = await JsonOf(await host.SendAsync(HttpMethod.Get, $"{Base}?pageSize=2&page=2", cookie: cookie));
        Assert.Equal(1, paged.GetProperty("items").GetArrayLength());
        Assert.Equal(3, paged.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, paged.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task List_SearchTreatsPercentAndUnderscoreAsPlainCharacters()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var category = ReadApiData.Category("figures");
        await SeedAsync(host, context => ReadApiData.AddAll(
            context, [category], [ReadApiData.Product(category, "a", "Dragon"), ReadApiData.Product(category, "b", "50% off")]));

        var all = await JsonOf(await host.SendAsync(HttpMethod.Get, $"{Base}?search=%25", cookie: cookie));

        Assert.Equal("b", Assert.Single(all.GetProperty("items").EnumerateArray()).GetProperty("slug").GetString());
    }

    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=0")]
    [InlineData("page=0")]
    [InlineData("status=Deleted")]
    public async Task List_WithBadParameters_Returns400(string query)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}?{query}", cookie: cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Fact]
    public async Task List_AllowsAPageOfOneHundred()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}?pageSize=100", cookie: cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(100, (await JsonOf(response)).GetProperty("pageSize").GetInt32());
    }

    // --- The shop sees the change ---

    [Fact]
    public async Task TheShop_ReflectsPublish_PriceChange_VariantDeactivation_Archive_AndDraft()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var categoryId = await SeedCategoryAsync(host);
        var created = await CreateProductAsync(host, cookie, categoryId, "dragon");
        var id = created.GetProperty("id").GetGuid();
        var withFirst = await AddVariantAsync(host, cookie, id, "DRG-1", price: 2000);
        await AddVariantAsync(host, cookie, id, "DRG-2", price: 3000);

        // A draft is not shown.
        Assert.Empty(await PublicSlugsAsync(host));
        Assert.Equal(HttpStatusCode.NotFound, await PublicProductStatusAsync(host, "dragon"));

        // Publishing shows it, with the lowest price of the active variants.
        var current = await GetProductAsync(host, cookie, id);
        Assert.Equal(HttpStatusCode.OK, (await SetStatusAsync(host, cookie, current, "Published")).StatusCode);
        Assert.Equal(["dragon"], await PublicSlugsAsync(host));
        Assert.Equal(2000, await PublicMinPriceAsync(host));

        // A price change is visible.
        var firstId = withFirst.GetProperty("variants")[0].GetProperty("id").GetGuid();
        var edit = new
        {
            sku = "DRG-1",
            name = "DRG-1",
            price = 1750L,
            weightGrams = 100,
            compareAtPrice = (long?)null,
            stockQuantity = 5,
            attributes = new Dictionary<string, string>(),
        };
        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(HttpMethod.Put, $"{Base}/{id}/variants/{firstId}", edit, cookie)).StatusCode);
        Assert.Equal(1750, await PublicMinPriceAsync(host));

        // A deactivated variant disappears from the page and from the lowest price.
        Assert.Equal(
            HttpStatusCode.OK,
            (await host.SendAsync(HttpMethod.Put, $"{Base}/{id}/variants/{firstId}/active", new { isActive = false }, cookie)).StatusCode);
        var details = await JsonOf(await host.SendAsync(HttpMethod.Get, "/api/v1/products/dragon"));
        Assert.Equal(["DRG-2"], details.GetProperty("variants").EnumerateArray().Select(v => v.GetProperty("sku").GetString()!));
        Assert.Equal(3000, await PublicMinPriceAsync(host));

        // Archiving and going back to draft hide the product.
        current = await GetProductAsync(host, cookie, id);
        Assert.Equal(HttpStatusCode.OK, (await SetStatusAsync(host, cookie, current, "Archived")).StatusCode);
        Assert.Empty(await PublicSlugsAsync(host));
        Assert.Equal(HttpStatusCode.NotFound, await PublicProductStatusAsync(host, "dragon"));

        current = await GetProductAsync(host, cookie, id);
        Assert.Equal(HttpStatusCode.OK, (await SetStatusAsync(host, cookie, current, "Published")).StatusCode);
        Assert.Equal(["dragon"], await PublicSlugsAsync(host));
        current = await GetProductAsync(host, cookie, id);
        Assert.Equal(HttpStatusCode.OK, (await SetStatusAsync(host, cookie, current, "Draft")).StatusCode);
        Assert.Empty(await PublicSlugsAsync(host));
    }

    private static async Task<long> PublicMinPriceAsync(AuthApiHost host)
    {
        var body = await JsonOf(await host.SendAsync(HttpMethod.Get, "/api/v1/products?pageSize=48"));
        return body.GetProperty("items")[0].GetProperty("minPrice").GetInt64();
    }
}
