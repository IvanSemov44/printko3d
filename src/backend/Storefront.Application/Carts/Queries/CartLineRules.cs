using Storefront.Domain.Catalog;

namespace Storefront.Application.Carts.Queries;

public static class CartLineRules
{
    /// <summary>The simplest rule: later tasks refine it (stock limits on add, made-to-order lead time).</summary>
    public static CartLineState StateOf(
        ProductStatus productStatus,
        FulfilmentType fulfilmentType,
        bool isVariantActive,
        int? stockQuantity,
        int quantity)
    {
        if (productStatus == ProductStatus.Archived)
        {
            return CartLineState.Archived;
        }

        if (productStatus != ProductStatus.Published || !isVariantActive)
        {
            return CartLineState.Inactive;
        }

        return fulfilmentType == FulfilmentType.InStock && (stockQuantity ?? 0) < quantity
            ? CartLineState.InsufficientStock
            : CartLineState.Available;
    }
}
