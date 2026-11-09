using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Storefront.Application.Auth;
using Storefront.Infrastructure.Identity;

namespace Storefront.Api;

/// <summary>
/// The wiring for the administrator login: Identity, the session cookie, closed-by-default authorization,
/// the limit on sign-in attempts and the CSRF header check.
/// </summary>
internal static class AdminAuthSetup
{
    public const string LoginRateLimit = "login";

    /// <summary>Every request that changes data must carry this header. A browser sends it cross-site only after a CORS check, which the API fails.</summary>
    public const string CsrfHeader = "X-Requested-With";

    public static void AddAdminAuth(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection(AdminLoginOptions.SectionName).Get<AdminLoginOptions>()
            ?? new AdminLoginOptions();
        builder.Services.Configure<AdminLoginOptions>(builder.Configuration.GetSection(AdminLoginOptions.SectionName));

        // Decision 0013: password-only login must not reach production by mistake.
        if (options.Enabled)
        {
            AdminLoginEnvironments.EnsureAllowed(builder.Environment.EnvironmentName);
        }

        builder.Services.AddAdminIdentity().AddAdminSignIn();
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies(_ => { });
        builder.Services.ConfigureApplicationCookie(cookie =>
        {
            // The __Host- prefix makes the browser accept the cookie only if it is Secure, has Path=/ and no Domain.
            cookie.Cookie.Name = "__Host-printko-admin";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.Cookie.Path = "/";
            cookie.ExpireTimeSpan = TimeSpan.FromHours(8);
            cookie.SlidingExpiration = true;
            cookie.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            cookie.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        // Check the security stamp on every request, so a signed-out cookie stops working at once.
        builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);

        // Closed by default: an address without [AllowAnonymous] or [Authorize] needs the Admin role.
        builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireRole(AdminRole.Name).Build());

        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(LoginRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.LoginAttemptsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
    }

    public static IApplicationBuilder UseCsrfHeaderCheck(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var method = context.Request.Method;
            var changesData = !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method)
                || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method));
            if (changesData && !context.Request.Headers.ContainsKey(CsrfHeader))
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Extensions = { ["code"] = "csrf.header_required" },
                };
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(
                    new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
                return;
            }

            await next(context);
        });
}
