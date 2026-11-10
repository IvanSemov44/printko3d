namespace Storefront.Application.Catalog;

/// <summary>Everything the admin form can change on a variant. Active state and order have their own operations.</summary>
public sealed record UpdateVariant(
    string Sku,
    string Name,
    long Price,
    int WeightGrams,
    long? CompareAtPrice = null,
    int? StockQuantity = null,
    IReadOnlyDictionary<string, string>? Attributes = null);
