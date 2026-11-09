using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storefront.Domain.Catalog;
using Storefront.IntegrationTests.Persistence;
using Storefront.IntegrationTests.ReadApi;

namespace Storefront.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class AdminCategoriesApiTests(PostgresFixture postgres)
{
    private const string Base = "/api/v1/admin/categories";
    private static readonly string Id = "00000000-0000-0000-0000-000000000001";

    private static object NewCategory(string slug = "figures", string name = "Figures", object? parentId = null) =>
        new { name, slug, parentId, description = (string?)null, sortOrder = 0, isActive = true };

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> CreateAsync(
        AuthApiHost host, string cookie, string slug, string? name = null, Guid? parentId = null)
    {
        var response = await host.SendAsync(
            HttpMethod.Post, Base, NewCategory(slug, name ?? slug, parentId), cookie);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await JsonOf(response);
    }

    private static object Update(JsonElement category, string? name = null, string? slug = null, object? parentId = null) =>
        new
        {
            name = name ?? category.GetProperty("name").GetString(),
            slug = slug ?? category.GetProperty("slug").GetString(),
            parentId = parentId ?? (category.GetProperty("parentId").ValueKind == JsonValueKind.Null
                ? null
                : category.GetProperty("parentId").GetGuid()),
            description = (string?)null,
            sortOrder = category.GetProperty("sortOrder").GetInt32(),
            isActive = category.GetProperty("isActive").GetBoolean(),
            version = category.GetProperty("version").GetUInt32(),
        };

    // --- Who may call ---

    public static TheoryData<string, string> Addresses => new()
    {
        { "GET", Base },
        { "GET", $"{Base}/{Id}" },
        { "POST", Base },
        { "PUT", $"{Base}/{Id}" },
        { "POST", $"{Base}/{Id}/activate" },
        { "POST", $"{Base}/{Id}/deactivate" },
        { "POST", $"{Base}/{Id}/move" },
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
    public async Task Create_ReturnsADto_AndTheListShowsIt()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var created = await CreateAsync(host, cookie, "figures", "Figures");
        var list = await JsonOf(await host.SendAsync(HttpMethod.Get, Base, cookie: cookie));

        Assert.Equal("figures", created.GetProperty("slug").GetString());
        Assert.True(created.GetProperty("isActive").GetBoolean());
        Assert.Equal(0, created.GetProperty("productCount").GetInt32());
        Assert.False(created.TryGetProperty("createdAt", out _));
        Assert.Equal(created.GetProperty("id").GetGuid(), Assert.Single(list.EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task List_ContainsInactiveCategories_WithTheirProductCount()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var hidden = ReadApiData.Category("hidden", isActive: false);
        await SeedAsync(host, context =>
            ReadApiData.AddAll(context, [hidden], [ReadApiData.Product(hidden, "a"), ReadApiData.Product(hidden, "b")]));

        var list = await JsonOf(await host.SendAsync(HttpMethod.Get, Base, cookie: cookie));

        var only = Assert.Single(list.EnumerateArray());
        Assert.False(only.GetProperty("isActive").GetBoolean());
        Assert.Equal(2, only.GetProperty("productCount").GetInt32());
    }

    [Fact]
    public async Task Get_OfAnUnknownCategory_Returns404WithACode()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}/{Id}", cookie: cookie);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.IsProblemJson());
        Assert.Equal("category.not_found", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Update_ChangesTheCategory_AndReturnsTheNewVersion()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateAsync(host, cookie, "figures", "Figures");

        var response = await host.SendAsync(
            HttpMethod.Put, $"{Base}/{created.GetProperty("id").GetGuid()}", Update(created, name: "Фигури"), cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await JsonOf(response);
        Assert.Equal("Фигури", updated.GetProperty("name").GetString());
        Assert.NotEqual(created.GetProperty("version").GetUInt32(), updated.GetProperty("version").GetUInt32());
    }

    // --- Input and rule errors ---

    [Theory]
    [InlineData("", "Figures", "slug")]
    [InlineData("Bad Slug", "Figures", "slug")]
    [InlineData("figures", "", "name")]
    public async Task Create_WithInvalidInput_Returns400WithTheFieldInTheErrors(string slug, string name, string field)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Post, Base, NewCategory(slug, name), cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
        var body = await JsonOf(response);
        Assert.Equal("validation.failed", body.GetProperty("code").GetString());
        Assert.Contains(
            body.GetProperty("errors").EnumerateObject(),
            e => string.Equals(e.Name, field, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_WithATakenSlug_Returns409WithACode()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        await CreateAsync(host, cookie, "figures");

        var response = await host.SendAsync(HttpMethod.Post, Base, NewCategory("figures", "Other"), cookie);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(response.IsProblemJson());
        Assert.Equal("category.slug_taken", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Create_WithAnUnknownParent_Returns404WithACode()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(
            HttpMethod.Post, Base, NewCategory("dragons", "Dragons", Guid.CreateVersion7()), cookie);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("category.parent_not_found", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Create_UnderASubcategory_Returns400()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var root = await CreateAsync(host, cookie, "figures");
        var child = await CreateAsync(host, cookie, "dragons", parentId: root.GetProperty("id").GetGuid());

        var response = await host.SendAsync(
            HttpMethod.Post, Base, NewCategory("baby", "Baby", child.GetProperty("id").GetGuid()), cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ManyCreatesOfTheSameSlugAtOnce_GiveExactlyOneCategory_AndNeverA500()
    {
        // Two requests can both pass the "is the slug free" check; the unique index in the database must then decide.
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            host.SendAsync(HttpMethod.Post, Base, NewCategory("figures", $"Figures {i}"), cookie)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var context = host.Database.CreateContext();
        Assert.Equal(1, await context.Categories.CountAsync());
    }

    // --- Two people editing at once ---

    [Fact]
    public async Task Update_WithAStaleVersion_Returns409WithACode_AndKeepsTheOtherEdit()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateAsync(host, cookie, "figures", "Figures");
        var url = $"{Base}/{created.GetProperty("id").GetGuid()}";

        var first = await host.SendAsync(HttpMethod.Put, url, Update(created, name: "First"), cookie);
        var second = await host.SendAsync(HttpMethod.Put, url, Update(created, name: "Second"), cookie);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("category.version_conflict", (await JsonOf(second)).GetProperty("code").GetString());
        var current = await JsonOf(await host.SendAsync(HttpMethod.Get, url, cookie: cookie));
        Assert.Equal("First", current.GetProperty("name").GetString());
    }

    [Fact]
    public async Task TwoUpdatesWithTheSameVersionAtOnce_GiveOneSuccessAndOneConflict()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateAsync(host, cookie, "figures", "Figures");
        var url = $"{Base}/{created.GetProperty("id").GetGuid()}";

        var responses = await Task.WhenAll(
            host.SendAsync(HttpMethod.Put, url, Update(created, name: "A"), cookie),
            host.SendAsync(HttpMethod.Put, url, Update(created, name: "B"), cookie));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Deactivate_WithAStaleVersion_Returns409()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateAsync(host, cookie, "figures");
        var url = $"{Base}/{created.GetProperty("id").GetGuid()}";
        await host.SendAsync(HttpMethod.Put, url, Update(created, name: "Changed"), cookie);

        var response = await host.SendAsync(
            HttpMethod.Post, $"{url}/deactivate", new { version = created.GetProperty("version").GetUInt32() }, cookie);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("category.version_conflict", (await JsonOf(response)).GetProperty("code").GetString());
    }

    // --- Move ---

    [Fact]
    public async Task Move_ChangesTheOrderOfTheList_AndReturnsFreshVersions()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        await CreateAsync(host, cookie, "a");
        var b = await CreateAsync(host, cookie, "b");
        await CreateAsync(host, cookie, "c");
        var list = await JsonOf(await host.SendAsync(HttpMethod.Get, Base, cookie: cookie));
        var current = list.EnumerateArray().Single(x => x.GetProperty("slug").GetString() == "b");

        var response = await host.SendAsync(
            HttpMethod.Post,
            $"{Base}/{b.GetProperty("id").GetGuid()}/move",
            new { direction = "Up", version = current.GetProperty("version").GetUInt32() },
            cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = (await JsonOf(response)).EnumerateArray().Select(x => x.GetProperty("slug").GetString()!).ToArray();
        Assert.Equal(["b", "a", "c"], moved);
    }

    [Fact]
    public async Task Move_WithAnUnknownDirection_Returns400()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var created = await CreateAsync(host, cookie, "a");

        var response = await host.SendAsync(
            HttpMethod.Post,
            $"{Base}/{created.GetProperty("id").GetGuid()}/move",
            new { direction = "Sideways", version = created.GetProperty("version").GetUInt32() },
            cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- The shop sees the change ---

    [Fact]
    public async Task TheShop_ReflectsCreate_Reorder_AndDeactivate()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var figures = await CreateAsync(host, cookie, "figures", "Figures");
        var toys = await CreateAsync(host, cookie, "toys", "Toys");
        var figuresId = figures.GetProperty("id").GetGuid();
        await SeedAsync(host, context =>
        {
            var category = context.Categories.Single(c => c.Id == figuresId);
            context.Products.Add(ReadApiData.Product(category, "dragon"));
        });

        // Created categories show up in the shop, in sortOrder order.
        Assert.Equal(["figures", "toys"], await PublicCategorySlugsAsync(host));
        Assert.Equal(["dragon"], await PublicProductSlugsAsync(host));

        // Reordering is visible.
        var toysNow = await GetAsync(host, cookie, toys.GetProperty("id").GetGuid());
        await host.SendAsync(
            HttpMethod.Post,
            $"{Base}/{toysNow.GetProperty("id").GetGuid()}/move",
            new { direction = "Up", version = toysNow.GetProperty("version").GetUInt32() },
            cookie);
        Assert.Equal(["toys", "figures"], await PublicCategorySlugsAsync(host));

        // Deactivating hides the category and its products. Nothing is deleted.
        var figuresNow = await GetAsync(host, cookie, figuresId);
        var deactivated = await host.SendAsync(
            HttpMethod.Post,
            $"{Base}/{figuresId}/deactivate",
            new { version = figuresNow.GetProperty("version").GetUInt32() },
            cookie);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.Equal(["toys"], await PublicCategorySlugsAsync(host));
        Assert.Empty(await PublicProductSlugsAsync(host));
        await using (var context = host.Database.CreateContext())
        {
            Assert.Equal(1, await context.Products.CountAsync());
        }

        // Activating brings them back.
        var inactive = await GetAsync(host, cookie, figuresId);
        await host.SendAsync(
            HttpMethod.Post,
            $"{Base}/{figuresId}/activate",
            new { version = inactive.GetProperty("version").GetUInt32() },
            cookie);
        Assert.Contains("figures", await PublicCategorySlugsAsync(host));
        Assert.Equal(["dragon"], await PublicProductSlugsAsync(host));
    }

    [Fact]
    public async Task DeactivatingAParent_HidesTheSubcategoryToo_ButKeepsIt()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var root = await CreateAsync(host, cookie, "figures");
        await CreateAsync(host, cookie, "dragons", parentId: root.GetProperty("id").GetGuid());

        await host.SendAsync(
            HttpMethod.Post,
            $"{Base}/{root.GetProperty("id").GetGuid()}/deactivate",
            new { version = root.GetProperty("version").GetUInt32() },
            cookie);

        Assert.Empty(await PublicCategorySlugsAsync(host));
        var adminList = await JsonOf(await host.SendAsync(HttpMethod.Get, Base, cookie: cookie));
        Assert.Equal(2, adminList.GetArrayLength());
    }

    private static async Task<JsonElement> GetAsync(AuthApiHost host, string cookie, Guid id) =>
        await JsonOf(await host.SendAsync(HttpMethod.Get, $"{Base}/{id}", cookie: cookie));

    private static async Task<string[]> PublicCategorySlugsAsync(AuthApiHost host)
    {
        var json = await JsonOf(await host.SendAsync(HttpMethod.Get, "/api/v1/categories"));
        return json.EnumerateArray().Select(c => c.GetProperty("slug").GetString()!).ToArray();
    }

    private static async Task<string[]> PublicProductSlugsAsync(AuthApiHost host)
    {
        var json = await JsonOf(await host.SendAsync(HttpMethod.Get, "/api/v1/products"));
        return json.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("slug").GetString()!).ToArray();
    }

    private static async Task SeedAsync(AuthApiHost host, Action<Storefront.Infrastructure.Persistence.StorefrontDbContext> add)
    {
        await using var context = host.Database.CreateContext();
        add(context);
        await context.SaveChangesAsync();
    }
}
