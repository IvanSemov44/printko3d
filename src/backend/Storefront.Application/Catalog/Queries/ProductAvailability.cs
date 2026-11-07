using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog.Queries;

public static class ProductAvailability
{
    /// <summary>A made-to-order product can always be ordered; an in-stock one needs stock in an active variant.</summary>
    public static bool IsAvailable(FulfilmentType fulfilmentType, bool anyActiveVariantInStock) =>
        fulfilmentType == FulfilmentType.MadeToOrder || anyActiveVariantInStock;

    public static bool IsVariantAvailable(FulfilmentType fulfilmentType, int? stockQuantity) =>
        IsAvailable(fulfilmentType, stockQuantity > 0);
}
