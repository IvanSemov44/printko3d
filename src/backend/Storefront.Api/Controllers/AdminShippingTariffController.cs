using Microsoft.AspNetCore.Mvc;
using Storefront.Api.Requests;
using Storefront.Application.Catalog;
using Storefront.Application.Shipping;

namespace Storefront.Api.Controllers;

/// <summary>
/// The tariff of delivery to the address for the admin panel (decision 0034). No attribute is needed here: every address
/// without [AllowAnonymous] already needs a signed-in administrator. The buyer reads the prices at /api/v1/shipping/quotes.
/// </summary>
[ApiController]
[Route("api/v1/admin/shipping-tariff")]
public sealed class AdminShippingTariffController(IServiceManager services) : ControllerBase
{
    /// <summary>The steps from the lightest to the heaviest. An empty list means no delivery is offered.</summary>
    [HttpGet]
    [ProducesResponseType<ShippingTariffDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ShippingTariffDto>> Get(CancellationToken cancellationToken) =>
        Ok(await services.ShippingTariff.GetAsync(cancellationToken));

    /// <summary>
    /// Replaces the whole tariff and returns it sorted by weight (200, not 204: the panel shows what was saved). The
    /// steps may come in any order. Orders already placed keep the price they were made with.
    /// </summary>
    /// <remarks>Errors: 400 validation.failed (the steps are named), 409 shipping.tariff_conflict.</remarks>
    [HttpPut]
    [ProducesResponseType<ShippingTariffDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShippingTariffDto>> Replace(
        ReplaceShippingTariffRequest request, CancellationToken cancellationToken) =>
        Ok(await services.ShippingTariff.ReplaceAsync(request.ToModel(), cancellationToken));
}
