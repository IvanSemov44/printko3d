using Storefront.Domain.Catalog;
using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

/// <summary>
/// The lines of an order that hold stock, in the order of the variant ids, so that two orders with the same lines never
/// wait for each other in a circle (decision 0033). Taking the stock, giving it back and taking it again after a late
/// payment all go through here, so they cannot disagree about which lines count.
/// </summary>
internal static class OrderStockLines
{
    public static IEnumerable<OrderItem> Of(Order order) =>
        order.Items.Where(i => i.FulfilmentType == FulfilmentType.InStock).OrderBy(i => i.VariantId);
}
