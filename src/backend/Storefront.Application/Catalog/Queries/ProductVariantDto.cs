namespace Storefront.Application.Catalog.Queries;

public sealed record ProductVariantDto(
    Guid Id,
    string Sku,
    string Name,
    long Price,
    long? CompareAtPrice,
    bool IsAvailable,
    int WeightGrams,
    IReadOnlyDictionary<string, string> Attributes,
    int SortOrder);
