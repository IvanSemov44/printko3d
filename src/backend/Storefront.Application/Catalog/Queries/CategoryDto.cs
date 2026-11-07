namespace Storefront.Application.Catalog.Queries;

public sealed record CategoryDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Slug,
    string? Description,
    string? ImageKey,
    int SortOrder);
