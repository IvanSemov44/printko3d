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
    /// <summary>Active categories, ordered by sortOrder.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<CategoryDto>> GetCategories(CancellationToken cancellationToken) =>
        await services.Catalog.GetCategoriesAsync(cancellationToken);
}
