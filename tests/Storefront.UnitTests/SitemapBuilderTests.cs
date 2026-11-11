using System.Xml.Linq;
using Storefront.Application.Catalog;
using Storefront.Application.Catalog.Queries;

namespace Storefront.UnitTests;

public sealed class SitemapBuilderTests
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private static List<XElement> Urls(string xml) => XDocument.Parse(xml).Root!.Elements(Ns + "url").ToList();

    private static SitemapEntryDto Entry(int number) =>
        new($"/products/p{number}", new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Build_StartsWithTheHomePage_ThenTheEntriesWithAbsoluteAddresses()
    {
        var xml = SitemapBuilder.Build(
            "https://shop.test/",
            [new SitemapEntryDto("/categories/figures", new DateTimeOffset(2026, 3, 1, 14, 30, 5, TimeSpan.FromHours(2)))]);

        var urls = Urls(xml);
        Assert.Equal(["https://shop.test/", "https://shop.test/categories/figures"], urls.Select(u => u.Element(Ns + "loc")!.Value));
        Assert.Null(urls[0].Element(Ns + "lastmod"));
        Assert.Equal("2026-03-01T12:30:05Z", urls[1].Element(Ns + "lastmod")!.Value);
    }

    [Fact]
    public void Build_DeclaresUtf8_BecauseTheTextIsSentAsUtf8()
    {
        var xml = SitemapBuilder.Build("https://shop.test", []);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", xml);
    }

    [Fact]
    public void Build_EscapesCharactersThatXmlReserves()
    {
        var xml = SitemapBuilder.Build("https://shop.test", [new SitemapEntryDto("/products/a&b", DateTimeOffset.UnixEpoch)]);

        Assert.Equal("https://shop.test/products/a&b", Urls(xml)[1].Element(Ns + "loc")!.Value);
        Assert.Contains("a&amp;b", xml);
    }

    [Fact]
    public void Build_KeepsAllAddressesAtTheLimit()
    {
        // The home page plus 49,999 entries is exactly 50,000 addresses.
        var xml = SitemapBuilder.Build("https://shop.test", Enumerable.Range(1, SitemapBuilder.MaxEntries).Select(Entry));

        Assert.Equal(SitemapBuilder.MaxUrls, Urls(xml).Count);
    }

    [Fact]
    public void Build_CutsOffAddressesBeyondTheLimit_KeepingTheFirstOnes()
    {
        var xml = SitemapBuilder.Build("https://shop.test", Enumerable.Range(1, SitemapBuilder.MaxEntries + 1).Select(Entry));

        var urls = Urls(xml);
        Assert.Equal(SitemapBuilder.MaxUrls, urls.Count);
        Assert.EndsWith($"/products/p{SitemapBuilder.MaxEntries}", urls[^1].Element(Ns + "loc")!.Value);
    }
}
