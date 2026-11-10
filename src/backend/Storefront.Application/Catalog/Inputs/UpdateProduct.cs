using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

/// <summary>
/// Everything the admin form can change on a product except the images and the status.
/// <see cref="Version"/> is the value the form was loaded with. <see cref="VariantStock"/> is the stock of every
/// variant, and it is required only when the product changes from made-to-order to in-stock: until then the
/// variants have no stock to keep.
/// </summary>
public sealed record UpdateProduct(
    Guid CategoryId,
    string Name,
    string Slug,
    string ShortDescription,
    string Description,
    FulfilmentType FulfilmentType,
    int? LeadTimeDays,
    string? SeoTitle,
    string? SeoDescription,
    uint Version,
    IReadOnlyDictionary<Guid, int>? VariantStock = null);
