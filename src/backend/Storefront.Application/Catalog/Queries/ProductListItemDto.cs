using System.Text.Json.Serialization;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog.Queries;

public sealed record ProductListItemDto(
    Guid Id,
    string Name,
    string Slug,
    string ShortDescription,
    [property: JsonConverter(typeof(JsonStringEnumConverter<FulfilmentType>))] FulfilmentType FulfilmentType,
    int? LeadTimeDays,
    long MinPrice,
    ImageRefDto? PrimaryImage,
    bool IsAvailable);
