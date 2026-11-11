using System.Net;
using System.Xml.Linq;
using Storefront.Domain.Catalog;
using Storefront.IntegrationTests.Persistence;
using static Storefront.IntegrationTests.ReadApi.ReadApiData;

namespace Storefront.IntegrationTests.ReadApi;

[Collection(PostgresCollection.Name)]
public sealed class SitemapTests(PostgresFixture postgres)
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private static async Task<List<XElement>> GetUrlsAsync(ReadApiHost host)
    {
        var response = await host.Client.GetAsync("/sitemap.xml");
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        return XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!.Elements(Ns + "url").ToList();
    }

    [Fact]
    public async Task Sitemap_ListsTheHomePage_VisibleCategories_AndPublishedProducts_WithAbsoluteAddresses()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres, "https://shop.test");
        var figures = Category("figures");
        var child = Category("small", parent: figures);
        var off = Category("off", isActive: false);
        var childOfOff = Category("child-of-off", parent: off);
        var dragon = Product(figures, "dragon");
        var draft = Product(figures, "draft", status: ProductStatus.Draft);
        var archived = Product(figures, "archived", status: ProductStatus.Archived);
        var inOff = Product(off, "in-off");
        var inChildOfOff = Product(childOfOff, "in-child-of-off");
        var noVariants = Product(figures, "no-variants");
        noVariants.Variants.Clear();
        var inactiveVariant = Product(figures, "inactive-variant");
        inactiveVariant.Variants.Single().IsActive = false;
        await host.SeedAsync(db => AddAll(
            db,
            [figures, child, off, childOfOff],
            [dragon, draft, archived, inOff, inChildOfOff, noVariants, inactiveVariant]));

        var locations = (await GetUrlsAsync(host)).Select(u => u.Element(Ns + "loc")!.Value);

        Assert.Equal(
            [
                "https://shop.test/",
                "https://shop.test/categories/figures",
                "https://shop.test/categories/small",
                "https://shop.test/products/dragon",
            ],
            locations);
    }

    [Fact]
    public async Task Sitemap_GivesTheLastChangeOfEachAddress()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres, "https://shop.test");
        var figures = Category("figures");
        var dragon = Product(figures, "dragon");
        await host.SeedAsync(db => AddAll(db, [figures], [dragon]));

        var urls = await GetUrlsAsync(host);

        // The save stamps UpdatedAt on the tracked entities; the sitemap shows it to the second, in UTC.
        Assert.Null(urls[0].Element(Ns + "lastmod"));
        Assert.Equal(LastModifiedText(figures.UpdatedAt), urls[1].Element(Ns + "lastmod")!.Value);
        Assert.Equal(LastModifiedText(dragon.UpdatedAt), urls[2].Element(Ns + "lastmod")!.Value);
    }

    private static string LastModifiedText(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Sitemap_Gives404_WhenTheAddressOfTheShopIsNotSet()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres);

        var response = await host.Client.GetAsync("/sitemap.xml");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Sitemap_NeedsTwoSqlCommands_NoMatterHowManyProducts()
    {
        await using var host = await ReadApiHost.CreateAsync(postgres, "https://shop.test");
        var figures = Category("figures");
        await host.SeedAsync(db => AddAll(db, [figures], Enumerable.Range(1, 5).Select(i => Product(figures, $"p{i}"))));

        Assert.Equal(2, await host.CountSqlAsync("/sitemap.xml"));
    }
}
