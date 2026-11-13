using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Api.Cart;
using Storefront.Api.Requests;
using Storefront.Application.Catalog;
using Storefront.Application.Orders;

namespace Storefront.Api.Controllers;

/// <summary>
/// Orders placed by a buyer without an account (decision 0032). The cart is the one of the `__Host-printko-cart` cookie.
/// Changes need the `X-Requested-With` header (403 csrf.header_required without it).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/orders")]
public sealed class OrdersController(IServiceManager services) : ControllerBase
{
    /// <summary>
    /// Turns the cart into an order and empties the cart. Prices, names and the total are read from the variants, not
    /// from the request. The answer is the order as it was placed. There is no address to read an order again yet, so
    /// there is no Location header: the buyer keeps what this answer says.
    /// </summary>
    /// <remarks>
    /// Errors: 400 validation.failed (a field is missing or wrong, or the terms are not accepted; the fields are named),
    /// 403 csrf.header_required, 409 order.total_changed (the cart is worth something else than `expectedTotal`: read the cart
    /// and ask again), 409 cart.changed (the cart was changed at the same moment: read it and try again),
    /// 422 order.cart_empty (no cart, an unknown cookie, or no lines), 422 order.cart_not_orderable (a line is archived,
    /// inactive or over the stock: remove it from the cart).
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderDto>> Place(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await services.Orders.PlaceAsync(CartCookie.Read(Request), request.ToInput(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, order);
    }
}
