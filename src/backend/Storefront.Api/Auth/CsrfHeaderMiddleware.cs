using Storefront.Api.Errors;

namespace Storefront.Api.Auth;

/// <summary>
/// Every request that changes data must carry the header `X-Requested-With` (decision 0014). A browser sends it
/// cross-site only after a CORS check, which the API fails, so a forged form post from another site is refused.
/// </summary>
internal static class CsrfHeaderMiddleware
{
    public const string HeaderName = "X-Requested-With";

    public static IApplicationBuilder UseCsrfHeaderCheck(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (ChangesData(context.Request.Method)
                && !context.Request.Headers.ContainsKey(HeaderName)
                && !IsProviderMessage(context.Request))
            {
                var problem = ApiProblems.Create(
                    StatusCodes.Status403Forbidden, "Forbidden", detail: null, code: "csrf.header_required");
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(
                    new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
                return;
            }

            await next(context);
        });

    /// <summary>
    /// The one known exception (decision 0035): the payment provider cannot send the header, so its address is protected by
    /// the signature of the message instead. Nothing else is exempt, and a signature that is not right changes nothing.
    /// </summary>
    internal const string ProviderMessagePath = "/api/v1/payments/notifications";

    private static bool IsProviderMessage(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
        && request.Path.Equals(ProviderMessagePath, StringComparison.OrdinalIgnoreCase);

    private static bool ChangesData(string method) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method));
}
