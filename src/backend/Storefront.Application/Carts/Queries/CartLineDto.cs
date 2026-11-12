using System.Text.Json.Serialization;
using Storefront.Application.Catalog.Queries;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Carts.Queries;

/// <summary>
/// Prices are whole numbers in the smallest unit of the shop currency and come from the variant, not from the line.
/// <paramref name="AvailableQuantity"/> is the most units the shopper can take (the stock of an in-stock variant); null when made to order.
/// </summary>
public sealed record CartLineDto(
    Guid VariantId,
    string ProductName,
    string ProductSlug,
    string VariantName,
    string Sku,
    ImageRefDto? Image,
    long UnitPrice,
    int Quantity,
    long LinePrice,
    [property: JsonConverter(typeof(JsonStringEnumConverter<CartLineState>))] CartLineState State,
    [property: JsonConverter(typeof(JsonStringEnumConverter<FulfilmentType>))] FulfilmentType FulfilmentType,
    int? LeadTimeDays,
    int? AvailableQuantity);
