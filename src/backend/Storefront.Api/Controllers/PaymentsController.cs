using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Storefront.Api.Requests;
using Storefront.Api.Setup;
using Storefront.Application.Catalog;
using Storefront.Application.Payments;

namespace Storefront.Api.Controllers;

/// <summary>
/// Paying an order (decision 0035). The buyer has no account, so the addresses are open; what stops abuse is that the
/// sum always comes from the order and that a message of the provider needs a valid signature. Card data never reaches
/// this API: the buyer pays at the provider.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1")]
public sealed class PaymentsController(IServiceManager services, ILogger<PaymentsController> logger) : ControllerBase
{
    public const string SignatureHeader = "X-Payment-Signature";

    /// <summary>The most a message of the provider may weigh. The real ones are a few hundred bytes.</summary>
    private const int MaxMessageBytes = 16 * 1024;

    /// <summary>
    /// Makes sure the order has a payment waiting at the provider and returns where to pay. A second call while the
    /// payment waits returns the same session (200, not 201: nothing new is made). The sum and the currency are the ones
    /// of the order; the session ends at the deadline of the order.
    /// </summary>
    /// <remarks>
    /// Errors: 403 csrf.header_required, 404 order.not_found, 404 payment.disabled (payment is turned off),
    /// 409 payment.order_not_payable (the order is paid, cancelled or past its deadline), 429 (too many requests from this address).
    /// </remarks>
    [HttpPut("orders/{number:int}/payment")]
    [EnableRateLimiting(OrderRateLimits.Lookup)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<PaymentSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentSessionDto>> Start(int number, CancellationToken cancellationToken) =>
        Ok(await services.Payments.StartAsync(number, cancellationToken));

    /// <summary>
    /// Whether the payment is a test one. The storefront shows the notice "test payment and delivery" for as long as this
    /// is true, so it goes away by itself when a real provider replaces the test one. False when payment is turned off.
    /// </summary>
    [HttpGet("payments/mode")]
    [ProducesResponseType<PaymentModeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentModeDto>> GetMode(CancellationToken cancellationToken) =>
        Ok(await services.Payments.GetModeAsync(cancellationToken));

    /// <summary>
    /// The state of a payment, by the random identifier from <c>PUT /api/v1/orders/{number}/payment</c>: only
    /// <c>Pending</c>, <c>Succeeded</c>, <c>Failed</c>, <c>PaidWithoutOrder</c> or <c>Expired</c>, nothing about the buyer.
    /// </summary>
    /// <remarks>Errors: 404 payment.not_found.</remarks>
    [HttpGet("payments/{paymentId:guid}")]
    [ProducesResponseType<PaymentStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentStatusDto>> GetStatus(Guid paymentId, CancellationToken cancellationToken) =>
        Ok(await services.Payments.GetStatusAsync(paymentId, cancellationToken));

    /// <summary>
    /// The address the payment provider calls (decision 0035). It carries no <c>X-Requested-With</c> header, because a
    /// provider cannot send one: the signature in <c>X-Payment-Signature</c>, checked on the raw body before anything is
    /// read, is the protection, and the body is limited to 16 KB. A message that was handled before, or is about a session
    /// we do not know, is answered 204 and changes nothing, so the provider stops repeating it.
    /// </summary>
    /// <remarks>Errors: 400 payment.message_rejected (the signature or the body is not right; the reason is not told), 413 (too large).</remarks>
    [HttpPost("payments/notifications")]
    [RequestSizeLimit(MaxMessageBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult> ReceiveNotification(CancellationToken cancellationToken)
    {
        // Kestrel enforces the attribute above; this answers the same way where the header says it up front.
        if (Request.ContentLength > MaxMessageBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers[SignatureHeader].FirstOrDefault();

        var report = await services.Payments.HandleNotificationAsync(new PaymentMessage(body, signature), cancellationToken);
        LogReport(report);
        return NoContent();
    }

    /// <summary>
    /// The test payment page ends a payment: the test provider signs a message about the session and it goes the same road
    /// as a real one, so the order becomes paid (or the payment fails) only through the signature check. Only with the test
    /// provider; a real provider has no such address. The sum is the one of the payment.
    /// </summary>
    /// <remarks>
    /// Errors: 400 validation.failed (the outcome), 403 csrf.header_required, 404 payment.not_found,
    /// 404 payment.disabled, 422 payment.session_expired (a payment after the session ended).
    /// </remarks>
    [HttpPost("payments/test/{reference}/complete")]
    [EnableRateLimiting(OrderRateLimits.Lookup)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> CompleteTest(
        string reference,
        CompleteTestPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var report = await services.Payments.CompleteTestPaymentAsync(reference, request.Outcome, cancellationToken);
        LogReport(report);
        return NoContent();
    }

    // Only the number of the order is written: it is public by design and says nothing about the buyer (rule 7).
    private void LogReport(PaymentNotificationReport report)
    {
        switch (report.Result)
        {
            case PaymentNotificationResult.PaidWithoutOrder:
                logger.LogWarning(
                    "A payment of order {OrderNumber} came after the order was cancelled and the stock is gone. Return the money by hand.",
                    report.OrderNumber);
                break;
            case PaymentNotificationResult.Revived:
                logger.LogInformation("A late payment of order {OrderNumber} took the stock again and paid the order.", report.OrderNumber);
                break;
            case PaymentNotificationResult.AmountMismatch:
                logger.LogError("A payment of order {OrderNumber} came with another sum than the order. The order was not paid.", report.OrderNumber);
                break;
            case PaymentNotificationResult.UnknownPayment:
                logger.LogWarning("A message of the payment provider was about a session we do not know.");
                break;
            default:
                break;
        }
    }
}
