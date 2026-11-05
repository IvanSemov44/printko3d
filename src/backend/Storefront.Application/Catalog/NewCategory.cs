namespace Storefront.Application.Catalog;

public sealed record NewCategory(
    string Name,
    string Slug,
    Guid? ParentId = null,
    string? Description = null,
    string? ImageKey = null,
    int SortOrder = 0,
    bool IsActive = true);
