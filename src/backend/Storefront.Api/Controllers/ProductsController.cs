using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Application.Catalog;
using Storefront.Application.Catalog.Queries;

namespace Storefront.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/products")]
public sealed class ProductsController(IServiceManager services) : ControllerBase
{
    /// <summary>A page of published products.</summary>
    /// <remarks>Errors: 400 validation.failed for a page, page size, sort or fulfilment type that is not allowed.</remarks>
    /// <param name="category">Slug of a category; also matches its subcategories. An unknown slug gives an empty page.</param>
    /// <param name="fulfilmentType">InStock or MadeToOrder.</param>
    /// <param name="sort">name (default), -name, price or -price. A leading minus means descending.</param>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Items per page, 1 to 48.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProductListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> GetProducts(
        [FromQuery] string? category,
        [FromQuery] string? fulfilmentType,
        [FromQuery] string? sort,
        CancellationToken cancellationToken,
        [FromQuery] int page = ProductListQuery.DefaultPage,
        [FromQuery] int pageSize = ProductListQuery.DefaultPageSize) =>
        Ok(await services.Catalog.GetProductsAsync(category, fulfilmentType, sort, page, pageSize, cancellationToken));

    /// <summary>One published product with its active variants and all images.</summary>
    /// <remarks>Errors: 404 product.not_found when the product is missing or not visible in the shop.</remarks>
    [HttpGet("{slug}")]
    [ProducesResponseType<ProductDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailsDto>> GetProduct(string slug, CancellationToken cancellationToken) =>
        Ok(await services.Catalog.GetProductAsync(slug, cancellationToken));
}
