using Storefront.Domain.Catalog;

namespace Storefront.Application.Contracts;

/// <summary>What the cart needs to know about a variant before it takes units of it.</summary>
/// <param name="IsOrderable">The variant is active and its product is published.</param>
public sealed record VariantSupply(bool IsOrderable, FulfilmentType FulfilmentType, int? StockQuantity)
{
    /// <summary>The most units that can be taken: the stock for an in-stock variant, none for one without stock, no limit when made to order.</summary>
    public int? Limit => FulfilmentType == FulfilmentType.InStock ? StockQuantity ?? 0 : null;
}
