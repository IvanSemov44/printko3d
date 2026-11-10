namespace Storefront.Application.Catalog.Queries;

/// <summary>One address for the sitemap: the path on the shop (for example /products/dragon) and when it last changed.</summary>
public sealed record SitemapEntryDto(string Path, DateTimeOffset LastModified);
