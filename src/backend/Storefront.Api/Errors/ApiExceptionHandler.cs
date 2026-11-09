using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Storefront.Application.Exceptions;

namespace Storefront.Api.Errors;

/// <summary>Turns the known Application exceptions into ProblemDetails. Anything else stays a generic 500.</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
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
            InvalidCredentialsException e => ApiProblems.Create(
                StatusCodes.Status401Unauthorized, "Sign-in failed", e.Message, "auth.invalid_credentials"),
            ValidationFailedException e => ApiProblems.Validation(e.Message, e.Errors),
            _ => null,
        };
        if (details is null)
        {
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
