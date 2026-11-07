using Microsoft.AspNetCore.Mvc;
using Storefront.Application.Catalog;
using Storefront.Application.Catalog.Queries;

namespace Storefront.Api.Controllers;

[ApiController]
[Route("api/v1/store-settings")]
public sealed class StoreSettingsController(IServiceManager services) : ControllerBase
{
    /// <summary>Store name, currency, locale, contacts and theme.</summary>
    [HttpGet]
    [ProducesResponseType<StoreSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<StoreSettingsDto> GetStoreSettings(CancellationToken cancellationToken) =>
        await services.Catalog.GetStoreSettingsAsync(cancellationToken);
}
