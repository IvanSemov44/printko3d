using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Api.Cart;
using Storefront.Api.Requests;
using Storefront.Application.Carts;
using Storefront.Application.Carts.Queries;
using Storefront.Application.Catalog;

namespace Storefront.Api.Controllers;

/// <summary>
/// The cart of an anonymous shopper, recognised by the `__Host-printko-cart` cookie (decision 0027). The cookie is
/// created by the first add and renewed by every change. A missing, expired or unknown cookie is an empty cart,
/// never an error. Changes need the `X-Requested-With` header (403 csrf.header_required without it).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/cart")]
public sealed class CartController(IServiceManager services) : ControllerBase
{
    /// <summary>The cart with the current price and state of every line. Never creates a cart.</summary>
    [HttpGet]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> Get(CancellationToken cancellationToken) =>
        Ok(await services.Cart.GetAsync(CartCookie.Read(Request), cancellationToken));

    /// <summary>One line of the cart: the address a new line is created at (Location of the 201 answer of the add).</summary>
    /// <remarks>Errors: 404 cart.item_not_found (no such line, or no cart).</remarks>
    [HttpGet("items/{variantId:guid}")]
    [ProducesResponseType<CartLineDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartLineDto>> GetItem(Guid variantId, CancellationToken cancellationToken) =>
        Ok(await services.Cart.GetItemAsync(CartCookie.Read(Request), variantId, cancellationToken));

    /// <summary>
    /// Adds units of a variant. The first add creates the cart and sets the cookie. A new line answers 201 with its
    /// address in Location; units added to a line that is already there answer 200. The body is the whole cart either way.
    /// </summary>
    /// <remarks>
    /// Errors: 400 validation.failed (quantity below 1), 403 csrf.header_required, 404 variant.not_found,
    /// 409 cart.item_conflict (two adds of the same new variant at once: try again),
    /// 422 cart.too_many_items (more than 50 different items), 422 cart.quantity_too_high (more than 20 units of one item),
    /// 422 cart.quantity_exceeds_stock (more units than are in stock, counting the ones already in the cart).
    /// </remarks>
    [HttpPost("items")]
    [ProducesResponseType<CartDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CartDto>> AddItem(AddCartItemRequest request, CancellationToken cancellationToken)
    {
        var change = await services.Cart.AddItemAsync(
            CartCookie.Read(Request), request.VariantId, request.Quantity, cancellationToken);
        KeepCookie(change);
        return change.LineCreated
            ? CreatedAtAction(nameof(GetItem), new { variantId = request.VariantId }, change.Cart)
            : Ok(change.Cart);
    }

    /// <summary>Replaces the number of units of one line.</summary>
    /// <remarks>
    /// Errors: 400 validation.failed (quantity below 1), 403 csrf.header_required, 404 cart.item_not_found,
    /// 422 cart.quantity_too_high, 422 cart.quantity_exceeds_stock (raising the quantity above the stock; lowering is always allowed).
    /// </remarks>
    [HttpPut("items/{variantId:guid}")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CartDto>> SetQuantity(
        Guid variantId, SetCartItemQuantityRequest request, CancellationToken cancellationToken) =>
        Respond(await services.Cart.SetQuantityAsync(
            CartCookie.Read(Request), variantId, request.Quantity, cancellationToken));

    /// <summary>Removes one line. The answer has no body: read the cart for its new state.</summary>
    /// <remarks>Errors: 403 csrf.header_required, 404 cart.item_not_found.</remarks>
    [HttpDelete("items/{variantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveItem(Guid variantId, CancellationToken cancellationToken)
    {
        KeepCookie(await services.Cart.RemoveItemAsync(CartCookie.Read(Request), variantId, cancellationToken));
        return NoContent();
    }

    /// <summary>Removes every line. The answer has no body. A shopper without a cart gets the same answer, not an error.</summary>
    /// <remarks>Errors: 403 csrf.header_required.</remarks>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Clear(CancellationToken cancellationToken)
    {
        KeepCookie(await services.Cart.ClearAsync(CartCookie.Read(Request), cancellationToken));
        return NoContent();
    }

    private ActionResult<CartDto> Respond(CartChange change)
    {
        KeepCookie(change);
        return Ok(change.Cart);
    }

    // A cart that exists keeps its cookie alive for another 30 days after every change.
    private void KeepCookie(CartChange change)
    {
        if (change.Token is not null)
        {
            CartCookie.Write(Response, change.Token);
        }
    }
}
