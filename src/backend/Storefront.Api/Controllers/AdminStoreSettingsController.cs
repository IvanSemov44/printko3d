using Microsoft.AspNetCore.Mvc;
using Storefront.Api.Requests;
using Storefront.Application.Catalog;
using Storefront.Application.Catalog.Queries;

namespace Storefront.Api.Controllers;

/// <summary>
/// Store settings for the admin panel. No attribute is needed here: every address without [AllowAnonymous]
/// already needs a signed-in administrator. The public read stays at /api/v1/store-settings.
/// </summary>
[ApiController]
[Route("api/v1/admin/store-settings")]
public sealed class AdminStoreSettingsController(IServiceManager services) : ControllerBase
{
    /// <summary>The settings with the row version the form must send back.</summary>
    /// <remarks>Errors: 404 store_settings.not_found.</remarks>
    [HttpGet]
    [ProducesResponseType<StoreSettingsAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StoreSettingsAdminDto>> Get(CancellationToken cancellationToken) =>
        Ok(await services.StoreSettings.GetAdminAsync(cancellationToken));

    /// <summary>Replaces the editable fields and returns the settings with their new version (200, not 204: the panel needs the new version).</summary>
    /// <remarks>Errors: 400 validation.failed, 404 store_settings.not_found, 409 store_settings.version_conflict or store_settings.currency_locked.</remarks>
    [HttpPut]
    [ProducesResponseType<StoreSettingsAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StoreSettingsAdminDto>> Update(
        UpdateStoreSettingsRequest request, CancellationToken cancellationToken) =>
        Ok(await services.StoreSettings.UpdateAsync(request.ToModel(), cancellationToken));
}
