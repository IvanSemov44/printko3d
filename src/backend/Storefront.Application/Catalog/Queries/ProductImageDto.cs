namespace Storefront.Application.Catalog.Queries;

public sealed record ProductImageDto(
    Guid Id,
    Guid? VariantId,
    string StorageKey,
    string AltText,
    int SortOrder,
    bool IsPrimary);
