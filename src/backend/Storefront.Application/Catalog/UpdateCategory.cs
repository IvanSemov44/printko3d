namespace Storefront.Application.Catalog;

/// <summary>Everything the admin form can change. <see cref="Version"/> is the value the form was loaded with.</summary>
public sealed record UpdateCategory(
    string Name,
    string Slug,
    Guid? ParentId,
    string? Description,
    int SortOrder,
    bool IsActive,
    uint Version);
