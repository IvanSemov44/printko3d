using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Application.Catalog;
using Storefront.Application.Catalog.Queries;

namespace Storefront.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/store-settings")]
public sealed class StoreSettingsController(IServiceManager services) : ControllerBase
{
    /// <summary>Store name, currency, locale, contacts and theme.</summary>
    /// <remarks>Errors: 404 store_settings.not_found when the settings row is missing.</remarks>
    [HttpGet]
    [ProducesResponseType<StoreSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StoreSettingsDto>> GetStoreSettings(CancellationToken cancellationToken) =>
        Ok(await services.Catalog.GetStoreSettingsAsync(cancellationToken));
}
