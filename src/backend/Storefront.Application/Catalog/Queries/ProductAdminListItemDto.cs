using System.Text.Json.Serialization;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog.Queries;

/// <summary>One row of the admin product list. Price and stock count only the active variants.</summary>
/// <param name="MinPrice">The lowest price of the active variants, or null when none is active.</param>
/// <param name="TotalStock">The sum of the stock of the active variants, or null for a made-to-order product.</param>
public sealed record ProductAdminListItemDto(
    Guid Id,
    string Name,
    string Slug,
    Guid CategoryId,
    string CategoryName,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ProductStatus>))] ProductStatus Status,
    [property: JsonConverter(typeof(JsonStringEnumConverter<FulfilmentType>))] FulfilmentType FulfilmentType,
    int VariantCount,
    long? MinPrice,
    int? TotalStock,
    uint Version);
