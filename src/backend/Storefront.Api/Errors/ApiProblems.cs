using Microsoft.AspNetCore.Mvc;

namespace Storefront.Api.Errors;

/// <summary>
/// The one place that decides what an error answer looks like: the status, a short title and a stable
/// machine-readable `code` that clients can switch on. Nothing else in the API builds a ProblemDetails by hand.
/// </summary>
internal static class ApiProblems
{
    public const string ValidationCode = "validation.failed";

    public static ProblemDetails Create(int status, string title, string? detail, string code) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Extensions = { ["code"] = code },
        };

    /// <summary>A 400 whose `errors` map says which field is wrong.</summary>
    public static ValidationProblemDetails Validation(string detail, IReadOnlyDictionary<string, string[]> errors) =>
        new(errors.ToDictionary(e => e.Key, e => e.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid request",
            Detail = detail,
            Extensions = { ["code"] = ValidationCode },
        };
}
