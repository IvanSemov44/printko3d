namespace Storefront.Application.Catalog.Queries;

/// <summary>A category as the admin panel sees it: active or not, with its product count and row version.</summary>
public sealed record CategoryAdminDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Slug,
    string? Description,
    int SortOrder,
    bool IsActive,
    int ProductCount,
    uint Version);
