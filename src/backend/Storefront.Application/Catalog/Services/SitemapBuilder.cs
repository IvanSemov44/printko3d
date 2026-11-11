using System.Globalization;
using System.Text;
using System.Xml;
using Storefront.Application.Catalog.Queries;

namespace Storefront.Application.Catalog;

/// <summary>
/// Builds sitemap.xml. A sitemap may hold 50,000 addresses and 50 MB (sitemaps.org); a shop with one line per
/// product stays far below the size limit (about 150 bytes per address), so only the number of addresses is
/// enforced. A shop that outgrows it needs a sitemap index, which is a separate task.
/// </summary>
public static class SitemapBuilder
{
    public const int MaxUrls = 50_000;

    private const string Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <summary>How many entries to ask for: the home page takes one place.</summary>
    public const int MaxEntries = MaxUrls - 1;

    /// <summary>The home page first, then the entries in the given order. More than 50,000 addresses are cut off.</summary>
    public static string Build(string siteUrl, IEnumerable<SitemapEntryDto> entries)
    {
        var origin = siteUrl.TrimEnd('/');
        // The text is sent as UTF-8, so the declaration says so; a StringBuilder would write "utf-16".
        var settings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true };
        var text = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>").AppendLine();

        using (var writer = XmlWriter.Create(text, settings))
        {
            writer.WriteStartElement("urlset", Namespace);
            WriteUrl(writer, origin + "/", null);
            foreach (var entry in entries.Take(MaxEntries))
            {
                WriteUrl(writer, origin + entry.Path, entry.LastModified);
            }

            writer.WriteEndElement();
        }

        return text.ToString();
    }

    private static void WriteUrl(XmlWriter writer, string location, DateTimeOffset? lastModified)
    {
        writer.WriteStartElement("url");
        writer.WriteElementString("loc", location);
        if (lastModified is { } modified)
        {
            writer.WriteElementString(
                "lastmod",
                modified.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        }

        writer.WriteEndElement();
    }
}
