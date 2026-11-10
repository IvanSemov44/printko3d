using Microsoft.AspNetCore.Mvc;
using Storefront.Api.Requests;
using Storefront.Application.Catalog;
using Storefront.Application.Catalog.Queries;

namespace Storefront.Api.Controllers;

/// <summary>
/// The images of a product for the admin panel. Like every address without [AllowAnonymous], these need a signed-in
/// administrator. Every call returns the whole product, as the variant calls do.
/// </summary>
[ApiController]
[Route("api/v1/admin/products/{id:guid}/images")]
public sealed class AdminProductImagesController(IServiceManager services) : ControllerBase
{
    // The file may be 5 MB; the rest of the form (text fields, boundaries) fits in the extra megabyte.
    private const long RequestLimit = ImageLimits.MaxBytes + (1024 * 1024);

    /// <summary>Uploads one image. The first image of a product becomes its primary one.</summary>
    /// <remarks>
    /// Errors: 400 validation.failed, image.empty or image.type_not_allowed; 404 product.not_found; 409 image.limit_reached;
    /// 413 image.too_large.
    /// </remarks>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimit)]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<ProductAdminDto>> Upload(
        Guid id, [FromForm] UploadImageRequest request, CancellationToken cancellationToken)
    {
        await using var content = request.File.OpenReadStream();
        await services.Product.UploadImageAsync(id, request.ToModel(content), cancellationToken);
        var product = await services.Product.GetAsync(id, cancellationToken);
        return CreatedAtAction(nameof(AdminProductsController.Get), "AdminProducts", new { id }, product);
    }

    /// <summary>Replaces the alt text and the variant of an image.</summary>
    /// <remarks>Errors: 400 validation.failed, 404 product.not_found or image.not_found.</remarks>
    [HttpPut("{imageId:guid}")]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductAdminDto>> Update(
        Guid id, Guid imageId, UpdateImageRequest request, CancellationToken cancellationToken)
    {
        await services.Product.UpdateImageAsync(id, imageId, request.ToModel(), cancellationToken);
        return Ok(await services.Product.GetAsync(id, cancellationToken));
    }

    /// <summary>Makes the image the primary one. The old primary image becomes an ordinary one.</summary>
    /// <remarks>Errors: 404 product.not_found or image.not_found, 409 image.primary_conflict.</remarks>
    [HttpPut("{imageId:guid}/primary")]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductAdminDto>> SetPrimary(
        Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        await services.Product.SetPrimaryImageAsync(id, imageId, cancellationToken);
        return Ok(await services.Product.GetAsync(id, cancellationToken));
    }

    /// <summary>Moves an image one place up or down and renumbers the images of the product 0, 1, 2...</summary>
    /// <remarks>Errors: 400 validation.failed, 404 product.not_found or image.not_found.</remarks>
    [HttpPost("{imageId:guid}/move")]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductAdminDto>> Move(
        Guid id, Guid imageId, MoveImageRequest request, CancellationToken cancellationToken)
    {
        await services.Product.MoveImageAsync(id, imageId, request.Direction, cancellationToken);
        return Ok(await services.Product.GetAsync(id, cancellationToken));
    }

    /// <summary>Deletes the image and its file. If it was the primary image, the first one in order becomes primary.</summary>
    /// <remarks>Errors: 404 product.not_found or image.not_found.</remarks>
    [HttpDelete("{imageId:guid}")]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductAdminDto>> Delete(
        Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        await services.Product.DeleteImageAsync(id, imageId, cancellationToken);
        return Ok(await services.Product.GetAsync(id, cancellationToken));
    }
}
