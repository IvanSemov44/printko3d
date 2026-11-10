using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Application.Catalog;
using Storefront.Application.Catalog.Queries;

namespace Storefront.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/categories")]
public sealed class CategoriesController(IServiceManager services) : ControllerBase
{
    /// <summary>Active categories, ordered by sortOrder. Without paging (decision 0009). Never returns an error code of its own.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories(CancellationToken cancellationToken) =>
        Ok(await services.Catalog.GetCategoriesAsync(cancellationToken));
}
