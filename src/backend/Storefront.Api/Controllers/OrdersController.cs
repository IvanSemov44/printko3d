using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Storefront.Api.Cart;
using Storefront.Api.Requests;
using Storefront.Api.Setup;
using Storefront.Application.Catalog;
using Storefront.Application.Orders;

namespace Storefront.Api.Controllers;

/// <summary>
/// Orders placed by a buyer without an account (decision 0032), and how the buyer sees them again (decision 0036). The cart is the one of the `__Host-printko-cart` cookie.
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
    /// and ask again), 409 order.stock_changed (someone took the last pieces at the same moment: read the cart and
    /// check it), 409 cart.changed (the cart was changed at the same moment: read it and try again),
    /// 422 order.cart_empty (no cart, an unknown cookie, or no lines), 422 order.cart_not_orderable (a line is archived,
    /// inactive or over the stock: remove it from the cart), 422 order.shipping_unavailable (the chosen delivery is not
    /// offered for this parcel: read the quotes and choose again), 422 shipping.too_heavy (the parcel is heavier than
    /// any delivery carries), 429 (too many orders from this cart in a minute). `expectedTotal` includes the delivery.
    /// </remarks>
    [HttpPost]
    [EnableRateLimiting(OrderRateLimits.Place)]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<OrderDto>> Place(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await services.Orders.PlaceAsync(CartCookie.Read(Request), request.ToInput(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, order);
    }

    /// <summary>
    /// Shows an order to the buyer who knows its number and its email (page "My order"). A wrong number and a wrong email
    /// look the same. It is a POST so that the email stays out of the address and the logs of the proxy. It changes nothing.
    /// </summary>
    /// <remarks>
    /// Errors: 400 validation.failed (a field is empty or too long), 403 csrf.header_required,
    /// 404 order.not_found (no such order, a wrong email, or too many wrong emails for the number: try again later),
    /// 429 (too many requests from this address).
    /// </remarks>
    [HttpPost("lookup")]
    [EnableRateLimiting(OrderRateLimits.Lookup)]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<OrderDto>> Lookup(OrderLookupRequest request, CancellationToken cancellationToken) =>
        Ok(await services.OrderLookup.FindAsync(request.Number, request.Email ?? string.Empty, cancellationToken));

    /// <summary>
    /// Shows the order that the key of the link in an email belongs to. The key is 256 random bits and is sent in the
    /// body, not in the address. It changes nothing.
    /// </summary>
    /// <remarks>
    /// Errors: 400 validation.failed (the token is empty or too long), 403 csrf.header_required, 404 order.not_found,
    /// 429 (too many requests from this address).
    /// </remarks>
    [HttpPost("lookup-by-token")]
    [EnableRateLimiting(OrderRateLimits.Lookup)]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<OrderDto>> LookupByToken(OrderTokenLookupRequest request, CancellationToken cancellationToken) =>
        Ok(await services.OrderLookup.FindByTokenAsync(request.Token ?? string.Empty, cancellationToken));
}
