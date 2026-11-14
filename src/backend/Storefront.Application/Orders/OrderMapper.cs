using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

/// <summary>Turns an order, with its lines, into the DTO. Entities never leave the Application project (rule 3).</summary>
internal static class OrderMapper
{
    public static OrderDto ToDto(Order order) => new(
        order.Number,
        order.Status.ToString(),
        order.Currency,
        order.TotalAmount,
        order.ShippingMethod,
        order.ShippingAmount,
        order.CustomerName,
        order.Email,
        order.Phone,
        order.ShippingStreet,
        order.ShippingCity,
        order.ShippingPostalCode,
        order.ShippingCountry,
        order.CreatedAt,
        order.PaymentDueAt,
        order.Items.Select(i => new OrderItemDto(
            i.ProductName,
            i.VariantName,
            i.Sku,
            i.UnitPrice,
            i.Quantity,
            i.LineTotal,
            i.FulfilmentType,
            i.LeadTimeDays)).ToList());
}
