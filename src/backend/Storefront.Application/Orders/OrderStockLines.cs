using Storefront.Application.Contracts;
using Storefront.Domain.Catalog;
using Storefront.Domain.Orders;

namespace Storefront.Application.Orders;

/// <summary>
/// The lines of an order that hold stock, and giving that stock back. The lines come in the order of the variant ids, so
/// that two orders with the same lines never wait for each other in a circle (decision 0033). Taking the stock, giving it
/// back after a refusal or an expiry, and taking it again after a late payment all go through here, so they cannot
/// disagree about which lines count.
/// </summary>
internal static class OrderStockLines
{
    public static IEnumerable<OrderItem> Of(Order order) =>
        order.Items.Where(i => i.FulfilmentType == FulfilmentType.InStock).OrderBy(i => i.VariantId);

    /// <summary>
    /// Records that the stock was given back and gives it back, once: the mark is one conditional statement, so only the
    /// caller that set it returns the pieces. Returns false when the stock was already given back. The caller owns the
    /// transaction. A variant that has no stock figure any more (made to order) is left as it is.
    /// </summary>
    public static async Task<bool> GiveBackAsync(IRepositoryManager repositories, Order order, CancellationToken cancellationToken)
    {
        if (!await repositories.Order.TryMarkStockReleasedAsync(order.Id, cancellationToken))
        {
            return false;
        }

        foreach (var item in Of(order))
        {
            await repositories.Product.ReturnStockAsync(item.VariantId, item.Quantity, cancellationToken);
        }

        return true;
    }
}
