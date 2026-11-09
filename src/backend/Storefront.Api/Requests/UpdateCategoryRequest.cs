using System.ComponentModel.DataAnnotations;
using Storefront.Application.Catalog;

namespace Storefront.Api.Requests;

/// <summary>The body of the request that replaces the editable fields of a category.</summary>
/// <param name="Version">The version the form was loaded with. A different value means someone else saved first.</param>
public sealed record UpdateCategoryRequest(
    [Required, StringLength(200)] string Name,
    [Required, StringLength(200)] string Slug,
    Guid? ParentId,
    [StringLength(2000)] string? Description,
    [Range(0, int.MaxValue)] int SortOrder,
    bool IsActive,
    uint Version)
{
    public UpdateCategory ToModel() => new(Name, Slug, ParentId, Description, SortOrder, IsActive, Version);
}
