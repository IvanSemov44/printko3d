using Microsoft.AspNetCore.Mvc;
using Storefront.Api.Requests;
using Storefront.Application.Catalog;
using Storefront.Application.Catalog.Queries;
using Storefront.Application.Orders;

namespace Storefront.Api.Controllers;

/// <summary>
/// Orders for the admin panel (task 28). No attribute is needed here: every address without [AllowAnonymous] already
/// needs a signed-in administrator. The answers hold the name, email, phone and address of the buyer, so nothing here is
/// written to the log.
/// </summary>
[ApiController]
[Route("api/v1/admin/orders")]
public sealed class AdminOrdersController(IServiceManager services) : ControllerBase
{
    /// <summary>A page of orders of every status, the newest first.</summary>
    /// <remarks>Errors: 400 validation.failed for a page, page size, status or search that is not allowed.</remarks>
    /// <param name="status">Names of statuses (New, Paid, InProduction, ReadyToShip, Shipped, Completed, Cancelled); repeat the parameter for more than one. Empty means every status.</param>
    /// <param name="search">The exact order number, or part of the email or the name, any letter case.</param>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Items per page, 1 to 100.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet]
    [ProducesResponseType<PagedResult<OrderAdminListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<OrderAdminListItemDto>>> GetAll(
        [FromQuery] string[]? status,
        [FromQuery] string? search,
        CancellationToken cancellationToken,
        [FromQuery] int page = OrderAdminListQuery.DefaultPage,
        [FromQuery] int pageSize = OrderAdminListQuery.DefaultPageSize) =>
        Ok(await services.OrderAdmin.GetListAsync(
            OrderAdminListQuery.Create(status, search, page, pageSize),
            cancellationToken));

    /// <summary>One order with its lines, the contact and the production steps that can be taken now.</summary>
    /// <remarks>Errors: 404 order.not_found.</remarks>
    [HttpGet("{number:int}")]
    [ProducesResponseType<OrderAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderAdminDto>> Get(int number, CancellationToken cancellationToken) =>
        Ok(await services.OrderAdmin.GetAsync(number, cancellationToken));

    /// <summary>
    /// Moves the order one production step forward (InProduction, ReadyToShip, Shipped, Completed) and returns it. Moving to
    /// Shipped queues the email to the buyer. Payment and refusal are not steps: see the cancel address.
    /// </summary>
    /// <remarks>Errors: 400 validation.failed (unknown status), 404 order.not_found, 409 order.status_transition_invalid.</remarks>
    [HttpPost("{number:int}/status")]
    [ProducesResponseType<OrderAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderAdminDto>> ChangeStatus(
        int number, ChangeOrderStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await services.OrderAdmin.ChangeStatusAsync(number, request.Status, cancellationToken));

    /// <summary>
    /// Refuses the order (from New, Paid, InProduction or ReadyToShip) and gives its stock back in the same transaction.
    /// <c>refundByHand</c> is true when the buyer had paid: the provider has no refund yet, so the money goes back outside
    /// the shop.
    /// </summary>
    /// <remarks>Errors: 404 order.not_found, 409 order.status_transition_invalid (shipped, completed or already cancelled).</remarks>
    [HttpPost("{number:int}/cancel")]
    [ProducesResponseType<OrderCancelledDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderCancelledDto>> Cancel(int number, CancellationToken cancellationToken) =>
        Ok(await services.OrderAdmin.CancelAsync(number, cancellationToken));
}
