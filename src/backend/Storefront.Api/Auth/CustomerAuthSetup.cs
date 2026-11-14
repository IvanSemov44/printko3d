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
        var authentication = builder.Services.AddAuthentication();
        authentication.AddCookie(CustomerAuthentication.Scheme, _ => { });
        builder.Services
            .AddOptions<CookieAuthenticationOptions>(CustomerAuthentication.Scheme)
            .Configure<IOptions<CustomerOptions>>(ConfigureCookie);
        builder.AddExternalProviders(authentication);
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

    /// <summary>
    /// Google and Facebook, each only when its keys are in the settings (decision 0039). What a provider says is kept for a few minutes in
    /// a cookie of its own, until the callback of the shop reads it.
    /// </summary>
    private static void AddExternalProviders(this WebApplicationBuilder builder, AuthenticationBuilder authentication)
    {
        var keys = builder.Configuration.GetSection(ExternalLoginOptions.SectionName).Get<ExternalLoginOptions>() ?? new ExternalLoginOptions();
        var enabled = new List<string>();

        authentication.AddCookie(CustomerAuthentication.ExternalScheme, cookie =>
        {
            cookie.Cookie.Name = CustomerAuthentication.ExternalCookieName;
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            // Lax: the buyer arrives from the provider, which is another site, with a plain navigation.
            cookie.Cookie.SameSite = SameSiteMode.Lax;
            cookie.Cookie.Path = "/";
            cookie.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        });

        if (keys.Google.IsSet)
        {
            authentication.AddGoogle(ExternalLoginOptions.GoogleName, options =>
            {
                options.ClientId = keys.Google.ClientId;
                options.ClientSecret = keys.Google.ClientSecret;
                options.SignInScheme = CustomerAuthentication.ExternalScheme;
                // Google says whether it verified the address; only a verified one makes or joins an account.
                options.ClaimActions.MapJsonKey("email_verified", "email_verified");
            });
            enabled.Add(ExternalLoginOptions.GoogleName);
        }

        if (keys.Facebook.IsSet)
        {
            authentication.AddFacebook(ExternalLoginOptions.FacebookName, options =>
            {
                options.AppId = keys.Facebook.AppId;
                options.AppSecret = keys.Facebook.AppSecret;
                options.SignInScheme = CustomerAuthentication.ExternalScheme;
            });
            enabled.Add(ExternalLoginOptions.FacebookName);
        }

        builder.Services.AddSingleton(new EnabledExternalProviders(enabled));
        builder.Services.AddSingleton<ExternalLinkIntent>();
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
