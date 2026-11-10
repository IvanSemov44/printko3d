using Microsoft.AspNetCore.Mvc;
using Storefront.Api.Requests;
using Storefront.Application.Catalog;
using Storefront.Application.Catalog.Queries;

namespace Storefront.Api.Controllers;

/// <summary>
/// Product and variant management for the admin panel. No attribute is needed here: every address without
/// [AllowAnonymous] already needs a signed-in administrator. Products are never deleted, only archived;
/// variants are never deleted, only deactivated.
/// </summary>
[ApiController]
[Route("api/v1/admin/products")]
public sealed class AdminProductsController(IServiceManager services) : ControllerBase
{
    /// <summary>A page of products of every status, ordered by name.</summary>
    /// <remarks>Errors: 400 validation.failed for a page, page size, status or search that is not allowed.</remarks>
    /// <param name="status">Draft, Published or Archived. Empty means every status.</param>
    /// <param name="categoryId">Only the products directly in this category.</param>
    /// <param name="search">Part of the name, any letter case.</param>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Items per page, 1 to 100.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProductAdminListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<ProductAdminListItemDto>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        CancellationToken cancellationToken,
        [FromQuery] int page = ProductAdminListQuery.DefaultPage,
        [FromQuery] int pageSize = ProductAdminListQuery.DefaultPageSize) =>
        Ok(await services.Product.GetListAsync(
            ProductAdminListQuery.Create(status, categoryId, search, page, pageSize),
            cancellationToken));

    /// <summary>One product with all its variants, the inactive ones too.</summary>
    /// <remarks>Errors: 404 product.not_found.</remarks>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductAdminDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await services.Product.GetAsync(id, cancellationToken));

    /// <summary>Creates a product as a draft without variants.</summary>
    /// <remarks>Errors: 400 validation.failed, 404 product.category_not_found, 409 product.slug_taken.</remarks>
    [HttpPost]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductAdminDto>> Create(
        CreateProductRequest request, CancellationToken cancellationToken)
    {
        var id = await services.Product.CreateAsync(request.ToModel(), cancellationToken);
        var created = await services.Product.GetAsync(id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, created);
    }

    /// <summary>
    /// Replaces the editable fields and returns the product with its new version (200, not 204: the panel needs the new version).
    /// Changing the fulfilment type also changes the variants: to MadeToOrder their stock is cleared, and to InStock the
    /// stock of every variant must be sent in variantStock.
    /// </summary>
    /// <remarks>Errors: 400 validation.failed, 404 product.not_found or product.category_not_found, 409 product.slug_taken or product.version_conflict.</remarks>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductAdminDto>> Update(
        Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        await services.Product.UpdateAsync(id, request.ToModel(), cancellationToken);
        return Ok(await services.Product.GetAsync(id, cancellationToken));
    }

    /// <summary>Publishes, archives or returns the product to draft. Archiving and draft hide it from the shop.</summary>
    /// <remarks>Errors: 404 product.not_found, 409 product.version_conflict or product.cannot_publish_without_active_variant.</remarks>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductAdminDto>> SetStatus(
        Guid id, SetProductStatusRequest request, CancellationToken cancellationToken)
    {
        await services.Product.SetStatusAsync(id, request.Status, request.Version, cancellationToken);
        return Ok(await services.Product.GetAsync(id, cancellationToken));
    }

    /// <summary>Adds a variant and returns the product with all its variants. The version of the product does not change.</summary>
    /// <remarks>Errors: 400 validation.failed, 404 product.not_found, 409 variant.sku_taken.</remarks>
    [HttpPost("{id:guid}/variants")]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductAdminDto>> AddVariant(
        Guid id, CreateVariantRequest request, CancellationToken cancellationToken)
    {
        await services.Product.AddVariantAsync(id, request.ToModel(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, await services.Product.GetAsync(id, cancellationToken));
    }

    /// <summary>Replaces the editable fields of a variant and returns the product with all its variants.</summary>
    /// <remarks>Errors: 400 validation.failed, 404 product.not_found or variant.not_found, 409 variant.sku_taken.</remarks>
    [HttpPut("{id:guid}/variants/{variantId:guid}")]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductAdminDto>> UpdateVariant(
        Guid id, Guid variantId, UpdateVariantRequest request, CancellationToken cancellationToken)
    {
        await services.Product.UpdateVariantAsync(id, variantId, request.ToModel(), cancellationToken);
        return Ok(await services.Product.GetAsync(id, cancellationToken));
    }

    /// <summary>Shows or hides a variant in the shop. The last active variant of a published product cannot be hidden.</summary>
    /// <remarks>Errors: 404 product.not_found or variant.not_found, 409 variant.last_active.</remarks>
    [HttpPut("{id:guid}/variants/{variantId:guid}/active")]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductAdminDto>> SetVariantActive(
        Guid id, Guid variantId, SetVariantActiveRequest request, CancellationToken cancellationToken)
    {
        await services.Product.SetVariantActiveAsync(id, variantId, request.IsActive, cancellationToken);
        return Ok(await services.Product.GetAsync(id, cancellationToken));
    }

    /// <summary>Moves a variant one place up or down and renumbers the variants of the product 0, 1, 2... Returns the product.</summary>
    /// <remarks>Errors: 400 validation.failed, 404 product.not_found or variant.not_found.</remarks>
    [HttpPost("{id:guid}/variants/{variantId:guid}/move")]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductAdminDto>> MoveVariant(
        Guid id, Guid variantId, MoveVariantRequest request, CancellationToken cancellationToken)
    {
        await services.Product.MoveVariantAsync(id, variantId, request.Direction, cancellationToken);
        return Ok(await services.Product.GetAsync(id, cancellationToken));
    }
}
