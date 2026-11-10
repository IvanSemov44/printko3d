using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Storefront.Application.Auth;
using Storefront.Infrastructure.Identity;

namespace Storefront.Api.Auth;

/// <summary>
/// The wiring for the administrator login: Identity, the session cookie, closed-by-default authorization
/// and the limit on sign-in attempts. The CSRF header check is in <see cref="CsrfHeaderMiddleware"/>.
/// </summary>
internal static class AdminAuthSetup
{
    public const string LoginRateLimit = "login";

    public static void AddAdminAuth(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(AdminLoginOptions.SectionName);
        var options = section.Get<AdminLoginOptions>() ?? new AdminLoginOptions();
        builder.Services.Configure<AdminLoginOptions>(section);

        // Decision 0013: password-only login must not reach production by mistake.
        if (options.Enabled)
        {
            AdminLoginEnvironments.EnsureAllowed(builder.Environment.EnvironmentName);
        }

        builder.Services.AddAdminIdentity().AddAdminSignIn();
        builder.Services.AddAdminSessionCookie();
        builder.Services.AddClosedByDefaultAuthorization();
        builder.Services.AddLoginRateLimit(options.LoginAttemptsPerMinute);
    }

    private static void AddAdminSessionCookie(this IServiceCollection services)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies(_ => { });
        services.ConfigureApplicationCookie(cookie =>
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
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
    }

    /// <summary>Closed by default: an address without [AllowAnonymous] or [Authorize] needs the Admin role.</summary>
    private static void AddClosedByDefaultAuthorization(this IServiceCollection services) =>
        services.AddAuthorizationBuilder().SetFallbackPolicy(
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireRole(AdminRole.Name).Build());

    private static void AddLoginRateLimit(this IServiceCollection services, int attemptsPerMinute) =>
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(LoginRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = attemptsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
}
