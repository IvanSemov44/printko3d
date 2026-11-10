using Microsoft.AspNetCore.Mvc;
using Storefront.Api.Requests;
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
    public async Task<ActionResult<IReadOnlyList<CategoryAdminDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await services.Category.GetAllAsync(cancellationToken));

    /// <summary>One category.</summary>
    /// <remarks>Errors: 404 category.not_found.</remarks>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CategoryAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryAdminDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await services.Category.GetAsync(id, cancellationToken));

    /// <summary>Creates a category.</summary>
    /// <remarks>Errors: 400 validation.failed, 404 category.parent_not_found, 409 category.slug_taken.</remarks>
    [HttpPost]
    [ProducesResponseType<CategoryAdminDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryAdminDto>> Create(
        CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var id = await services.Category.CreateAsync(request.ToModel(), cancellationToken);
        var created = await services.Category.GetAsync(id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, created);
    }

    /// <summary>Replaces the editable fields and returns the category with its new version (200, not 204: the panel needs the new version).</summary>
    /// <remarks>Errors: 400 validation.failed, 404 category.not_found or category.parent_not_found, 409 category.slug_taken or category.version_conflict.</remarks>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<CategoryAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryAdminDto>> Update(
        Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        await services.Category.UpdateAsync(id, request.ToModel(), cancellationToken);
        return Ok(await services.Category.GetAsync(id, cancellationToken));
    }

    /// <summary>Shows the category in the shop again (its parent must be active as well for it to appear).</summary>
    /// <remarks>Errors: 404 category.not_found, 409 category.version_conflict.</remarks>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType<CategoryAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryAdminDto>> Activate(
        Guid id, CategoryVersionRequest request, CancellationToken cancellationToken)
    {
        await services.Category.SetActiveAsync(id, true, request.Version, cancellationToken);
        return Ok(await services.Category.GetAsync(id, cancellationToken));
    }

    /// <summary>Hides the category from the shop. Its products and subcategories stay in the database and are hidden too.</summary>
    /// <remarks>Errors: 404 category.not_found, 409 category.version_conflict.</remarks>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<CategoryAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryAdminDto>> Deactivate(
        Guid id, CategoryVersionRequest request, CancellationToken cancellationToken)
    {
        await services.Category.SetActiveAsync(id, false, request.Version, cancellationToken);
        return Ok(await services.Category.GetAsync(id, cancellationToken));
    }

    /// <summary>Moves the category one place up or down among the categories with the same parent, and renumbers that group 0, 1, 2... Returns the whole list with the new versions.</summary>
    /// <remarks>Errors: 400 validation.failed, 404 category.not_found, 409 category.version_conflict.</remarks>
    [HttpPost("{id:guid}/move")]
    [ProducesResponseType<IReadOnlyList<CategoryAdminDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<CategoryAdminDto>>> Move(
        Guid id, MoveCategoryRequest request, CancellationToken cancellationToken)
    {
        await services.Category.MoveAsync(id, request.Direction, request.Version, cancellationToken);
        return Ok(await services.Category.GetAllAsync(cancellationToken));
    }
}
