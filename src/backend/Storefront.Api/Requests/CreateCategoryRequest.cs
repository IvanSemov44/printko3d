using System.ComponentModel.DataAnnotations;
using Storefront.Application.Catalog;

namespace Storefront.Api.Requests;

/// <summary>The body of the request that creates a category. The image is set by a later task.</summary>
public sealed record CreateCategoryRequest(
    [Required, StringLength(200)] string Name,
    [Required, StringLength(200)] string Slug,
    Guid? ParentId,
    [StringLength(2000)] string? Description,
    [Range(0, int.MaxValue)] int SortOrder = 0,
    bool IsActive = true)
{
    public NewCategory ToModel() =>
        new(Name, Slug, ParentId, Description, SortOrder: SortOrder, IsActive: IsActive);
}
