using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Storefront.Application.Customers;
using Storefront.Infrastructure.Identity;

namespace Storefront.Api.Auth;

/// <summary>
/// The wiring for the buyer's account (decision 0039): Identity for <see cref="CustomerUser"/>, the buyer's own cookie scheme, which
/// is not the administrator's, and the limit on the open account addresses. The addresses of the administrator keep their closed
/// default (<see cref="AdminAuthSetup"/>): the buyer's cookie does not open them, and the administrator's does not open the buyer's.
/// </summary>
internal static class CustomerAuthSetup
{
    public const string RateLimit = "account";

    public static void AddCustomerAuth(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<CustomerOptions>()
            .Bind(builder.Configuration.GetSection(CustomerOptions.SectionName))
            .Validate(
                CustomerOptions.IsValid,
                "Customer values are out of range: TokenLifetimeMinutes 5 to 10080, AttemptsPerMinute 1 to 1000 and SessionDays 1 to 90.")
            .ValidateOnStart();
        builder.Services.AddSingleton(services => services.GetRequiredService<IOptions<CustomerOptions>>().Value);

        builder.Services.AddCustomerIdentity();
        // The link in an email works for as long as the setting says (the administrator has no such links).
        builder.Services
            .AddOptions<DataProtectionTokenProviderOptions>()
            .Configure<IOptions<CustomerOptions>>((token, customer) =>
                token.TokenLifespan = TimeSpan.FromMinutes(customer.Value.TokenLifetimeMinutes));
        builder.Services.AddAuthentication().AddCookie(CustomerAuthentication.Scheme, _ => { });
        builder.Services
            .AddOptions<CookieAuthenticationOptions>(CustomerAuthentication.Scheme)
            .Configure<IOptions<CustomerOptions>>(ConfigureCookie);
        builder.Services.AddRateLimiter(limiter => limiter.AddPolicy(RateLimit, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = context.RequestServices.GetRequiredService<CustomerOptions>().AttemptsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                })));
    }

    private static void ConfigureCookie(CookieAuthenticationOptions cookie, IOptions<CustomerOptions> customer)
    {
        // The __Host- prefix makes the browser accept the cookie only if it is Secure, has Path=/ and no Domain.
        cookie.Cookie.Name = CustomerAuthentication.CookieName;
        cookie.Cookie.HttpOnly = true;
        cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        // Lax, not Strict: the buyer comes back from a social provider, which is another site. Changes still need the CSRF header.
        cookie.Cookie.SameSite = SameSiteMode.Lax;
        cookie.Cookie.Path = "/";
        cookie.ExpireTimeSpan = TimeSpan.FromDays(customer.Value.SessionDays);
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
        // Check the security stamp on every request, so a signed-out or changed account stops working at once.
        cookie.Events.OnValidatePrincipal = async context =>
        {
            var signIn = context.HttpContext.RequestServices.GetRequiredService<SignInManager<CustomerUser>>();
            if (await signIn.ValidateSecurityStampAsync(context.Principal) is null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CustomerAuthentication.Scheme);
            }
        };
    }
}
