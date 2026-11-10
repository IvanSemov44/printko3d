using System.Text.Json.Serialization;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog.Queries;

/// <summary>A product as the admin form edits it, with all its variants and the row version.</summary>
public sealed record ProductAdminDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string Slug,
    string ShortDescription,
    string Description,
    [property: JsonConverter(typeof(JsonStringEnumConverter<FulfilmentType>))] FulfilmentType FulfilmentType,
    int? LeadTimeDays,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ProductStatus>))] ProductStatus Status,
    string? SeoTitle,
    string? SeoDescription,
    uint Version,
    IReadOnlyList<VariantAdminDto> Variants,
    IReadOnlyList<ProductImageDto> Images);
