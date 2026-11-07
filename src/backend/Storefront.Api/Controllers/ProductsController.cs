using Microsoft.AspNetCore.Mvc;
using Storefront.Application.Catalog;
using Storefront.Application.Catalog.Queries;

namespace Storefront.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController(IServiceManager services) : ControllerBase
{
    /// <summary>A page of published products.</summary>
    /// <param name="category">Slug of a category; also matches its subcategories. An unknown slug gives an empty page.</param>
    /// <param name="fulfilmentType">InStock or MadeToOrder.</param>
    /// <param name="sort">name (default), -name, price or -price. A leading minus means descending.</param>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Items per page, 1 to 48.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProductListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<PagedResult<ProductListItemDto>> GetProducts(
        [FromQuery] string? category,
        [FromQuery] string? fulfilmentType,
        [FromQuery] string? sort,
        CancellationToken cancellationToken,
        [FromQuery] int page = ProductListQuery.DefaultPage,
        [FromQuery] int pageSize = ProductListQuery.DefaultPageSize) =>
        await services.Catalog.GetProductsAsync(category, fulfilmentType, sort, page, pageSize, cancellationToken);

    /// <summary>One published product with its active variants and all images.</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType<ProductDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ProductDetailsDto> GetProduct(string slug, CancellationToken cancellationToken) =>
        await services.Catalog.GetProductAsync(slug, cancellationToken);
}
