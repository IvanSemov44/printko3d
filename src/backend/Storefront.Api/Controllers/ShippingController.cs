using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Api.Cart;
using Storefront.Application.Catalog;
using Storefront.Application.Shipping;

namespace Storefront.Api.Controllers;

/// <summary>
/// The delivery of the cart (decision 0034). The cart is the one of the `__Host-printko-cart` cookie; the address is
/// not needed to price the delivery, so the buyer sees the price before filling it in.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/shipping")]
public sealed class ShippingController(IServiceManager services) : ControllerBase
{
    /// <summary>
    /// The delivery methods for the cart and what each costs, with the total of the lines they are added to. The list
    /// is short (a handful of methods), so there is no paging. Never creates a cart.
    /// </summary>
    /// <remarks>
    /// Errors: 422 order.cart_empty (no cart, an unknown cookie, or no line that can be ordered),
    /// 422 shipping.too_heavy (the cart is heavier than any delivery carries).
    /// </remarks>
    [HttpGet("quotes")]
    [ProducesResponseType<ShippingOptionsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ShippingOptionsDto>> GetQuotes(CancellationToken cancellationToken) =>
        Ok(await services.Shipping.GetOptionsAsync(CartCookie.Read(Request), cancellationToken));
}
