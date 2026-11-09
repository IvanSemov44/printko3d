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
            if (ChangesData(context.Request.Method) && !context.Request.Headers.ContainsKey(HeaderName))
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

    private static bool ChangesData(string method) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method));
}
