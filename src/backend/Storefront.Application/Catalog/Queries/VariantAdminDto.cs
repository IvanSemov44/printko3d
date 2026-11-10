namespace Storefront.Application.Catalog.Queries;

/// <summary>A variant as the admin panel sees it: the inactive ones too, with the raw stock number.</summary>
public sealed record VariantAdminDto(
    Guid Id,
    string Sku,
    string Name,
    long Price,
    long? CompareAtPrice,
    int? StockQuantity,
    int WeightGrams,
    IReadOnlyDictionary<string, string> Attributes,
    bool IsActive,
    int SortOrder);
