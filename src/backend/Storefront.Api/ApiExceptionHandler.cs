using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Storefront.Application.Exceptions;

namespace Storefront.Api;

/// <summary>Turns the known Application exceptions into ProblemDetails. Anything else stays a generic 500.</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails details;
        switch (exception)
        {
            case NotFoundException notFound:
                details = new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not found",
                    Detail = notFound.Message,
                    Extensions = { ["code"] = notFound.Code },
                };
                break;
            case ValidationFailedException validation:
                details = new ValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value))
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid request",
                    Detail = validation.Message,
                    Extensions = { ["code"] = "validation.failed" },
                };
                break;
            default:
                return false;
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
