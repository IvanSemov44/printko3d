namespace Storefront.Application.Catalog;

public sealed record NewVariant(
    string Sku,
    string Name,
    long Price,
    int WeightGrams,
    long? CompareAtPrice = null,
    int? StockQuantity = null,
    IReadOnlyDictionary<string, string>? Attributes = null,
    bool IsActive = true,
    int SortOrder = 0);
