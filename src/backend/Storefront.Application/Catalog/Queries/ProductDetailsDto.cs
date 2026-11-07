using System.Text.Json.Serialization;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog.Queries;

public sealed record ProductDetailsDto(
    Guid Id,
    string Name,
    string Slug,
    string ShortDescription,
    string Description,
    [property: JsonConverter(typeof(JsonStringEnumConverter<FulfilmentType>))] FulfilmentType FulfilmentType,
    int? LeadTimeDays,
    string SeoTitle,
    string SeoDescription,
    bool IsAvailable,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductImageDto> Images);
