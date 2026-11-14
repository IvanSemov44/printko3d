using System.Text.Json.Serialization;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Orders;

/// <summary>
/// An order as it was placed. Money is whole numbers in the smallest unit of <paramref name="Currency"/>.
/// <paramref name="Total"/> is the lines plus <paramref name="ShippingAmount"/>, the price of the chosen delivery
/// <paramref name="ShippingMethod"/>.
/// </summary>
public sealed record OrderDto(
    int Number,
    string Status,
    string Currency,
    long Total,
    string ShippingMethod,
    long ShippingAmount,
    string CustomerName,
    string Email,
    string Phone,
    string ShippingStreet,
    string ShippingCity,
    string ShippingPostalCode,
    string ShippingCountry,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaymentDueAt,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderItemDto(
    string ProductName,
    string VariantName,
    string Sku,
    long UnitPrice,
    int Quantity,
    long LineTotal,
    [property: JsonConverter(typeof(JsonStringEnumConverter<FulfilmentType>))] FulfilmentType FulfilmentType,
    int? LeadTimeDays);
