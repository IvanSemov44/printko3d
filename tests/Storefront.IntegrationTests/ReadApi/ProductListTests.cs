using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Storefront.Domain.Catalog;
using Storefront.IntegrationTests.Persistence;
using static Storefront.IntegrationTests.ReadApi.ReadApiData;

namespace Storefront.IntegrationTests.ReadApi;

[Collection(PostgresCollection.Name)]
public sealed class ProductListTests(PostgresFixture postgres)
{
    private static string[] Slugs(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("slug").GetString()!).ToArray();

    [Fact]
    public async Task OnlyPublishedProductsInActiveCategories_AreListed()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var open = Category("open");
        var hidden = Category("hidden", isActive: false);
        var inactiveParent = Category("old-parent", isActive: false);
        var childOfInactive = Category("child-of-old", parent: inactiveParent);
        await host.SeedAsync(db => AddAll(
            db,
            [open, hidden, inactiveParent, childOfInactive],
            [
                Product(open, "published"),
                Product(open, "draft", status: ProductStatus.Draft),
                Product(open, "archived", status: ProductStatus.Archived),
                Product(hidden, "in-inactive-category"),
                Product(childOfInactive, "in-child-of-inactive-parent"),
            ]));

        var page = await host.GetJsonAsync("/api/v1/products");

        Assert.Equal(["published"], Slugs(page));
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ProductWithoutActiveVariants_IsNotListed()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        var product = Product(category, "no-active");
        product.Variants.Single().IsActive = false;
        await host.SeedAsync(db => AddAll(db, [category], [product, Product(category, "ok")]));

        var page = await host.GetJsonAsync("/api/v1/products");

        Assert.Equal(["ok"], Slugs(page));
    }

    [Fact]
    public async Task CategoryFilter_IncludesSubcategories_ButNotSiblings()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var figures = Category("figures");
        var dragons = Category("dragons", parent: figures);
        var decor = Category("decor");
        await host.SeedAsync(db => AddAll(
            db,
            [figures, dragons, decor],
            [Product(figures, "owl"), Product(dragons, "red-dragon"), Product(decor, "vase")]));

        Assert.Equal(["owl", "red-dragon"], Slugs(await host.GetJsonAsync("/api/v1/products?category=figures")));
        Assert.Equal(["red-dragon"], Slugs(await host.GetJsonAsync("/api/v1/products?category=dragons")));
        Assert.Equal(["vase"], Slugs(await host.GetJsonAsync("/api/v1/products?category=decor")));
    }

    [Fact]
    public async Task UnknownOrInactiveCategory_GivesAnEmptyPage()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var inactive = Category("inactive", isActive: false);
        await host.SeedAsync(db => AddAll(db, [inactive], [Product(inactive, "p")]));

        var unknown = await host.GetJsonAsync("/api/v1/products?category=nope");
        var inactivePage = await host.GetJsonAsync("/api/v1/products?category=inactive");

        Assert.Empty(Slugs(unknown));
        Assert.Equal(0, unknown.GetProperty("totalCount").GetInt32());
        Assert.Empty(Slugs(inactivePage));
    }

    [Fact]
    public async Task FulfilmentTypeFilter_KeepsOnlyThatType()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        await host.SeedAsync(db => AddAll(
            db,
            [category],
            [
                Product(category, "stocked"),
                Product(category, "made", fulfilmentType: FulfilmentType.MadeToOrder, stock: null),
            ]));

        Assert.Equal(["made"], Slugs(await host.GetJsonAsync("/api/v1/products?fulfilmentType=MadeToOrder")));
        Assert.Equal(["stocked"], Slugs(await host.GetJsonAsync("/api/v1/products?fulfilmentType=InStock")));
    }

    [Fact]
    public async Task Paging_SplitsTheListAndReportsTotals()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        await host.SeedAsync(db => AddAll(
            db,
            [category],
            Enumerable.Range(1, 5).Select(i => Product(category, $"p{i}"))));

        var second = await host.GetJsonAsync("/api/v1/products?page=2&pageSize=2");
        var beyond = await host.GetJsonAsync("/api/v1/products?page=9&pageSize=2");

        Assert.Equal(["p3", "p4"], Slugs(second));
        Assert.Equal(2, second.GetProperty("page").GetInt32());
        Assert.Equal(2, second.GetProperty("pageSize").GetInt32());
        Assert.Equal(5, second.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, second.GetProperty("totalPages").GetInt32());
        Assert.Empty(Slugs(beyond));
        Assert.Equal(5, beyond.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task DefaultPageSize_IsTwelve_AndMaximumIsFortyEight()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        await host.SeedAsync(db => AddAll(
            db,
            [category],
            Enumerable.Range(1, 50).Select(i => Product(category, $"p{i:00}"))));

        var byDefault = await host.GetJsonAsync("/api/v1/products");
        var maximum = await host.GetJsonAsync("/api/v1/products?pageSize=48");

        Assert.Equal(12, byDefault.GetProperty("pageSize").GetInt32());
        Assert.Equal(12, Slugs(byDefault).Length);
        Assert.Equal(48, Slugs(maximum).Length);
        Assert.Equal(2, maximum.GetProperty("totalPages").GetInt32());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-3")]
    [InlineData("page=abc")]
    [InlineData("pageSize=0")]
    [InlineData("page=10001")]
    [InlineData("page=2147483647")]
    [InlineData("pageSize=49")]
    [InlineData("pageSize=1000")]
    [InlineData("sort=cheapest")]
    [InlineData("fulfilmentType=Pickup")]
    public async Task InvalidParameters_Give400WithProblemDetails(string query)
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);

        var response = await host.Client.GetAsync($"/api/v1/products?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("validation.failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").EnumerateObject().Any());
        Assert.DoesNotContain("StackTrace", problem.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SortByName_IsAscendingByDefault_AndCanBeReversed()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        await host.SeedAsync(db => AddAll(
            db,
            [category],
            [Product(category, "s2", "Banana"), Product(category, "s1", "Apple"), Product(category, "s3", "Cherry")]));

        Assert.Equal(["s1", "s2", "s3"], Slugs(await host.GetJsonAsync("/api/v1/products")));
        Assert.Equal(["s1", "s2", "s3"], Slugs(await host.GetJsonAsync("/api/v1/products?sort=name")));
        Assert.Equal(["s3", "s2", "s1"], Slugs(await host.GetJsonAsync("/api/v1/products?sort=-name")));
    }

    [Fact]
    public async Task SortByPrice_UsesCheapestActiveVariant_AndBreaksTiesByName()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        var cheapWithDearVariant = Product(category, "cheap", "Cheap", price: 500);
        Variant(cheapWithDearVariant, "cheap-dear", price: 9000);
        var tieB = Product(category, "tie-b", "B", price: 2000);
        var tieA = Product(category, "tie-a", "A", price: 2000);
        var dearWithCheapInactive = Product(category, "dear", "Dear", price: 4000);
        Variant(dearWithCheapInactive, "dear-cheap-off", price: 1, isActive: false);
        await host.SeedAsync(db => AddAll(db, [category], [tieB, dearWithCheapInactive, tieA, cheapWithDearVariant]));

        var ascending = await host.GetJsonAsync("/api/v1/products?sort=price");
        var descending = await host.GetJsonAsync("/api/v1/products?sort=-price");

        Assert.Equal(["cheap", "tie-a", "tie-b", "dear"], Slugs(ascending));
        Assert.Equal(["dear", "tie-a", "tie-b", "cheap"], Slugs(descending));
    }

    [Fact]
    public async Task EqualNames_KeepTheSameOrderBetweenRequests_AndAcrossPages()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        await host.SeedAsync(db => AddAll(
            db,
            [category],
            Enumerable.Range(1, 6).Select(i => Product(category, $"twin-{i}", "Same name", price: 1000))));

        var first = Slugs(await host.GetJsonAsync("/api/v1/products?pageSize=6"));
        var second = Slugs(await host.GetJsonAsync("/api/v1/products?pageSize=6"));
        var pagedTwice = Slugs(await host.GetJsonAsync("/api/v1/products?pageSize=3"))
            .Concat(Slugs(await host.GetJsonAsync("/api/v1/products?pageSize=3&page=2")));

        Assert.Equal(first, second);
        Assert.Equal(first, pagedTwice);
        Assert.Equal(6, first.Distinct().Count());
    }

    [Fact]
    public async Task ListItem_HasMinPriceAvailabilityAndPrimaryImage_ButNoDetails()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        var product = Product(category, "owl", price: 3000, stock: 0);
        Variant(product, "owl-cheap", price: 1200, stock: 3);
        Variant(product, "owl-off", price: 100, stock: 9, isActive: false);
        Image(product, "owl/second", sortOrder: 2);
        Image(product, "owl/main", isPrimary: true, sortOrder: 5);
        await host.SeedAsync(db => AddAll(db, [category], [product]));

        var item = (await host.GetJsonAsync("/api/v1/products")).GetProperty("items")[0];

        Assert.Equal(1200, item.GetProperty("minPrice").GetInt64());
        Assert.True(item.GetProperty("isAvailable").GetBoolean());
        Assert.Equal("InStock", item.GetProperty("fulfilmentType").GetString());
        Assert.Equal("owl/main", item.GetProperty("primaryImage").GetProperty("storageKey").GetString());
        Assert.Equal("alt owl/main", item.GetProperty("primaryImage").GetProperty("altText").GetString());
        foreach (var hidden in new[] { "description", "variants", "images" })
        {
            Assert.False(item.TryGetProperty(hidden, out _), $"{hidden} must not be in the list");
        }
    }

    [Fact]
    public async Task ProductWithoutPrimaryImage_HasNullImage()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        await host.SeedAsync(db => AddAll(db, [category], [Product(category, "plain")]));

        var item = (await host.GetJsonAsync("/api/v1/products")).GetProperty("items")[0];

        Assert.Equal(JsonValueKind.Null, item.GetProperty("primaryImage").ValueKind);
    }

    [Theory]
    [InlineData(FulfilmentType.InStock, 5, true)]
    [InlineData(FulfilmentType.InStock, 0, false)]
    [InlineData(FulfilmentType.MadeToOrder, null, true)]
    public async Task Availability_FollowsFulfilmentTypeAndActiveStock(FulfilmentType type, int? stock, bool expected)
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        await host.SeedAsync(db => AddAll(db, [category], [Product(category, "p", fulfilmentType: type, stock: stock)]));

        var item = (await host.GetJsonAsync("/api/v1/products")).GetProperty("items")[0];

        Assert.Equal(expected, item.GetProperty("isAvailable").GetBoolean());
        if (type == FulfilmentType.MadeToOrder)
        {
            Assert.Equal(7, item.GetProperty("leadTimeDays").GetInt32());
        }
    }

    [Fact]
    public async Task StockInAnInactiveVariant_DoesNotMakeTheProductAvailable()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        var product = Product(category, "p", stock: 0);
        Variant(product, "p-off", stock: 9, isActive: false);
        await host.SeedAsync(db => AddAll(db, [category], [product]));

        var item = (await host.GetJsonAsync("/api/v1/products")).GetProperty("items")[0];

        Assert.False(item.GetProperty("isAvailable").GetBoolean());
    }

    [Fact]
    public async Task ListQueryCount_DoesNotGrowWithTheNumberOfProducts()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        var products = Enumerable.Range(1, 20).Select(i =>
        {
            var product = Product(category, $"p{i:00}");
            Variant(product, $"p{i:00}-2");
            Image(product, $"p{i:00}/main", isPrimary: true);
            return product;
        }).ToList();
        await host.SeedAsync(db => AddAll(db, [category], products));

        var commands = await host.CountSqlAsync("/api/v1/products?pageSize=20&sort=price&category=c");

        Assert.Equal(2, commands);
    }
}
