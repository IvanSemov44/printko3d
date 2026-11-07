using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Storefront.Domain.Catalog;
using Storefront.IntegrationTests.Persistence;
using static Storefront.IntegrationTests.ReadApi.ReadApiData;

namespace Storefront.IntegrationTests.ReadApi;

[Collection(PostgresCollection.Name)]
public sealed class ProductDetailsTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(ProductStatus.Draft)]
    [InlineData(ProductStatus.Archived)]
    public async Task NotPublishedProduct_Gives404WithProblemDetails(ProductStatus status)
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        await host.SeedAsync(db => AddAll(db, [category], [Product(category, "hidden", status: status)]));

        var response = await host.Client.GetAsync("/api/v1/products/hidden");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.Equal("product.not_found", problem.GetProperty("code").GetString());
        Assert.DoesNotContain("hidden", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ProductInInactiveCategory_Gives404()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c", isActive: false);
        await host.SeedAsync(db => AddAll(db, [category], [Product(category, "p")]));

        var response = await host.Client.GetAsync("/api/v1/products/p");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnknownSlug_Gives404_WithoutInternalDetails()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);

        var response = await host.Client.GetAsync("/api/v1/products/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
        Assert.DoesNotContain(" at Storefront", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Details_HaveFullDescriptionSeoAndActiveVariantsInOrder()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        var product = Product(category, "owl", "Owl", price: 2500, stock: 4);
        product.SeoTitle = "Owl figurine";
        var first = product.Variants.Single();
        first.SortOrder = 2;
        first.CompareAtPrice = 3000;
        Variant(product, "owl-small", price: 1500, stock: 0, sortOrder: 1);
        Variant(product, "owl-off", price: 1, stock: 9, isActive: false, sortOrder: 0);
        await host.SeedAsync(db => AddAll(db, [category], [product]));

        var details = await host.GetJsonAsync("/api/v1/products/owl");

        Assert.Equal("Long owl", details.GetProperty("description").GetString());
        Assert.Equal("Owl figurine", details.GetProperty("seoTitle").GetString());
        Assert.Equal("Short owl", details.GetProperty("seoDescription").GetString());
        Assert.True(details.GetProperty("isAvailable").GetBoolean());
        var variants = details.GetProperty("variants").EnumerateArray().ToArray();
        Assert.Equal(["owl-small", "owl-sku"], variants.Select(v => v.GetProperty("sku").GetString()));
        Assert.False(variants[0].GetProperty("isAvailable").GetBoolean());
        Assert.True(variants[1].GetProperty("isAvailable").GetBoolean());
        Assert.Equal(2500, variants[1].GetProperty("price").GetInt64());
        Assert.Equal(3000, variants[1].GetProperty("compareAtPrice").GetInt64());
        Assert.Equal(JsonValueKind.Null, variants[0].GetProperty("compareAtPrice").ValueKind);
        Assert.Equal(100, variants[1].GetProperty("weightGrams").GetInt32());
        Assert.Equal("blue", variants[1].GetProperty("attributes").GetProperty("color").GetString());
    }

    [Fact]
    public async Task MadeToOrderVariants_AreAvailable_AndShowLeadTime()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        await host.SeedAsync(db => AddAll(
            db,
            [category],
            [Product(category, "puzzle", fulfilmentType: FulfilmentType.MadeToOrder, stock: null)]));

        var details = await host.GetJsonAsync("/api/v1/products/puzzle");

        Assert.Equal("MadeToOrder", details.GetProperty("fulfilmentType").GetString());
        Assert.Equal(7, details.GetProperty("leadTimeDays").GetInt32());
        Assert.True(details.GetProperty("isAvailable").GetBoolean());
        Assert.True(details.GetProperty("variants")[0].GetProperty("isAvailable").GetBoolean());
    }

    [Fact]
    public async Task Images_AreOrdered_AndKeepTheirVariantLink()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        var product = Product(category, "owl");
        var red = Variant(product, "owl-red");
        Image(product, "owl/c", sortOrder: 3);
        Image(product, "owl/a", isPrimary: true, sortOrder: 1);
        Image(product, "owl/b", sortOrder: 2, variantId: red.Id);
        await host.SeedAsync(db => AddAll(db, [category], [product]));

        var images = (await host.GetJsonAsync("/api/v1/products/owl")).GetProperty("images").EnumerateArray().ToArray();

        Assert.Equal(["owl/a", "owl/b", "owl/c"], images.Select(i => i.GetProperty("storageKey").GetString()));
        Assert.Equal(JsonValueKind.Null, images[0].GetProperty("variantId").ValueKind);
        Assert.Equal(red.Id, images[1].GetProperty("variantId").GetGuid());
        Assert.True(images[0].GetProperty("isPrimary").GetBoolean());
        Assert.Equal("alt owl/a", images[0].GetProperty("altText").GetString());
    }

    [Fact]
    public async Task DetailsQueryCount_DoesNotGrowWithVariantsAndImages()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);
        var category = Category("c");
        var product = Product(category, "owl");
        for (var i = 0; i < 8; i++)
        {
            var variant = Variant(product, $"owl-{i}");
            Image(product, $"owl/{i}", sortOrder: i, variantId: variant.Id);
        }

        await host.SeedAsync(db => AddAll(db, [category], [product]));

        var commands = await host.CountSqlAsync("/api/v1/products/owl");

        Assert.Equal(1, commands);
    }
}
