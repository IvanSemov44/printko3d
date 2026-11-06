namespace Storefront.Application.Catalog;

public sealed record NewImage(
    string StorageKey,
    string AltText,
    Guid? VariantId = null,
    int SortOrder = 0,
    bool IsPrimary = false);
