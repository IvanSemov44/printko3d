using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Storefront.Application.Exceptions;
using Storefront.Application.Orders;

namespace Storefront.Api.Errors;

/// <summary>Turns the known Application exceptions into ProblemDetails. Anything else stays a generic 500.</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails? details = exception switch
        {
            NotFoundException e => ApiProblems.Create(StatusCodes.Status404NotFound, "Not found", e.Message, e.Code),
            ConflictException e => ApiProblems.Create(StatusCodes.Status409Conflict, "Conflict", e.Message, e.Code),
            RuleViolationException e => ApiProblems.Create(
                StatusCodes.Status422UnprocessableEntity, "Request not allowed", e.Message, e.Code),
            InvalidCredentialsException e => ApiProblems.Create(
                StatusCodes.Status401Unauthorized, "Sign-in failed", e.Message, "auth.invalid_credentials"),
            FileRejectedException e => ApiProblems.Create(
                e.TooLarge ? StatusCodes.Status413PayloadTooLarge : StatusCodes.Status400BadRequest,
                e.TooLarge ? "File too large" : "File not accepted",
                e.Message,
                e.Code),
            ValidationFailedException e => ApiProblems.Validation(e.Message, e.Errors),
            _ => null,
        };
        if (details is null)
        {
            return false;
        }

        if (exception is ConflictException { Code: OrderErrorCodes.StockChanged })
        {
            // Counted from the journal; nothing about the buyer or the cart is written.
            logger.LogInformation("An order was refused because the stock changed at the same moment.");
        }

        httpContext.Response.StatusCode = details.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = details,
            Exception = exception,
        });
    }
}
