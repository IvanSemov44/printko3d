using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Storefront.IntegrationTests.Persistence;
using static Storefront.IntegrationTests.ReadApi.ReadApiData;

namespace Storefront.IntegrationTests.ReadApi;

[Collection(PostgresCollection.Name)]
public sealed class CategoriesSettingsAndOpenApiTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Categories_ListsOnlyActiveOnes_InSortOrder()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var second = Category("second", sortOrder: 2);
        var first = Category("first", sortOrder: 1);
        var child = Category("child", parent: first, sortOrder: 3);
        var off = Category("off", isActive: false, sortOrder: 0);
        var childOfOff = Category("child-of-off", parent: off, sortOrder: 0);
        await host.SeedAsync(db => AddAll(db, [second, first, child, off, childOfOff], []));

        var categories = (await host.GetJsonAsync("/api/v1/categories")).EnumerateArray().ToArray();

        Assert.Equal(["first", "second", "child"], categories.Select(c => c.GetProperty("slug").GetString()));
        Assert.Equal(first.Id, categories[2].GetProperty("parentId").GetGuid());
        Assert.Equal(JsonValueKind.Null, categories[0].GetProperty("parentId").ValueKind);
        foreach (var name in new[] { "id", "name", "description", "imageKey", "sortOrder" })
        {
            Assert.True(categories[0].TryGetProperty(name, out _), name);
        }
    }

    [Fact]
    public async Task StoreSettings_AreReturned()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        await host.SeedAsync(db => db.StoreSettings.Add(CatalogData.Settings()));

        var settings = await host.GetJsonAsync("/api/v1/store-settings");

        Assert.Equal("Printko 3D", settings.GetProperty("storeName").GetString());
        Assert.Equal("BGN", settings.GetProperty("defaultCurrency").GetString());
        Assert.Equal("bg", settings.GetProperty("defaultLocale").GetString());
        Assert.Equal("shop@example.com", settings.GetProperty("contactEmail").GetString());
        Assert.Equal("#112233", settings.GetProperty("primaryColor").GetString());
        Assert.False(settings.GetProperty("isDemo").GetBoolean());
        Assert.Equal(JsonValueKind.Null, settings.GetProperty("logoKey").ValueKind);
    }

    [Fact]
    public async Task StoreSettings_Give404_WhenMissing()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);

        var response = await host.Client.GetAsync("/api/v1/store-settings");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CategoriesAndSettings_NeedOneSqlCommandEach()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        await host.SeedAsync(db => db.StoreSettings.Add(CatalogData.Settings()));

        Assert.Equal(1, await host.CountSqlAsync("/api/v1/categories"));
        Assert.Equal(1, await host.CountSqlAsync("/api/v1/store-settings"));
    }

    [Fact]
    public async Task OpenApiDocument_DescribesTheFourAddressesAndProblemResponses()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);

        var document = await host.Client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        var paths = document.GetProperty("paths");
        foreach (var path in new[]
                 {
                     "/api/v1/categories",
                     "/api/v1/products",
                     "/api/v1/products/{slug}",
                     "/api/v1/store-settings",
                 })
        {
            Assert.True(paths.TryGetProperty(path, out var item), path);
            Assert.True(item.TryGetProperty("get", out _), path);
        }

        var list = paths.GetProperty("/api/v1/products").GetProperty("get");
        var parameters = list.GetProperty("parameters").EnumerateArray()
            .Select(p => p.GetProperty("name").GetString()).ToArray();
        Assert.Equivalent(new[] { "category", "fulfilmentType", "sort", "page", "pageSize" }, parameters);
        Assert.True(list.GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(paths.GetProperty("/api/v1/products/{slug}").GetProperty("get")
            .GetProperty("responses").TryGetProperty("404", out _));
        Assert.True(paths.GetProperty("/api/v1/store-settings").GetProperty("get")
            .GetProperty("responses").TryGetProperty("404", out _));
    }
}
