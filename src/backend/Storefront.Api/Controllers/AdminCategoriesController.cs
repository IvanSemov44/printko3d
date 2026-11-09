using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Application.Catalog;
using Storefront.Application.Catalog.Queries;

namespace Storefront.Api.Controllers;

/// <summary>
/// Category management for the admin panel. No attribute is needed here: every address without
/// [AllowAnonymous] already needs a signed-in administrator. Categories are never deleted, only deactivated.
/// </summary>
[ApiController]
[Route("api/v1/admin/categories")]
public sealed class AdminCategoriesController(IServiceManager services) : ControllerBase
{
    /// <summary>All categories, the inactive ones too, ordered by sortOrder. Without paging, like the public list (decision 0016).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryAdminDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IReadOnlyList<CategoryAdminDto>> GetAll(CancellationToken cancellationToken) =>
        await services.Category.GetAllAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CategoryAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<CategoryAdminDto> Get(Guid id, CancellationToken cancellationToken) =>
        await services.Category.GetAsync(id, cancellationToken);

    /// <summary>Creates a category. 409 when the slug is taken, 404 when the parent does not exist.</summary>
    [HttpPost]
    [ProducesResponseType<CategoryAdminDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<CreatedAtActionResult> Create(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var id = await services.Category.CreateAsync(
            new NewCategory(
                request.Name,
                request.Slug,
                request.ParentId,
                request.Description,
                SortOrder: request.SortOrder,
                IsActive: request.IsActive),
            cancellationToken);
        var created = await services.Category.GetAsync(id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, created);
    }

    /// <summary>Replaces the editable fields. 409 with code category.version_conflict when someone saved the category after the form was loaded.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<CategoryAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<CategoryAdminDto> Update(
        Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        await services.Category.UpdateAsync(
            id,
            new UpdateCategory(
                request.Name,
                request.Slug,
                request.ParentId,
                request.Description,
                request.SortOrder,
                request.IsActive,
                request.Version),
            cancellationToken);
        return await services.Category.GetAsync(id, cancellationToken);
    }

    /// <summary>Shows the category in the shop again (its parent must be active as well for it to appear).</summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType<CategoryAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<CategoryAdminDto> Activate(
        Guid id, CategoryVersionRequest request, CancellationToken cancellationToken)
    {
        await services.Category.SetActiveAsync(id, true, request.Version, cancellationToken);
        return await services.Category.GetAsync(id, cancellationToken);
    }

    /// <summary>Hides the category from the shop. Its products and subcategories stay in the database and are hidden too.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<CategoryAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<CategoryAdminDto> Deactivate(
        Guid id, CategoryVersionRequest request, CancellationToken cancellationToken)
    {
        await services.Category.SetActiveAsync(id, false, request.Version, cancellationToken);
        return await services.Category.GetAsync(id, cancellationToken);
    }

    /// <summary>Moves the category one place up or down among the categories with the same parent, and renumbers that group 0, 1, 2... Returns the whole list with the new versions.</summary>
    [HttpPost("{id:guid}/move")]
    [ProducesResponseType<IReadOnlyList<CategoryAdminDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IReadOnlyList<CategoryAdminDto>> Move(
        Guid id, MoveCategoryRequest request, CancellationToken cancellationToken)
    {
        await services.Category.MoveAsync(id, request.Direction, request.Version, cancellationToken);
        return await services.Category.GetAllAsync(cancellationToken);
    }
}

public sealed record CreateCategoryRequest(
    [Required, StringLength(200)] string Name,
    [Required, StringLength(200)] string Slug,
    Guid? ParentId,
    [StringLength(2000)] string? Description,
    [Range(0, int.MaxValue)] int SortOrder = 0,
    bool IsActive = true);

public sealed record UpdateCategoryRequest(
    [Required, StringLength(200)] string Name,
    [Required, StringLength(200)] string Slug,
    Guid? ParentId,
    [StringLength(2000)] string? Description,
    [Range(0, int.MaxValue)] int SortOrder,
    bool IsActive,
    uint Version);

/// <summary>The version the caller last saw. A different value means someone else saved first.</summary>
public sealed record CategoryVersionRequest(uint Version);

public sealed record MoveCategoryRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] MoveDirection Direction,
    uint Version);
