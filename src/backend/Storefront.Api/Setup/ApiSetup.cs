using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using Storefront.Api.Auth;
using Storefront.Api.Background;
using Storefront.Api.Cart;
using Storefront.Api.Errors;
using Storefront.Application.Carts;
using Storefront.Application.Catalog;
using Storefront.Application.Contracts;
using Storefront.Application.Emails;
using Storefront.Application.Health;
using Storefront.Application.Orders;
using Storefront.Application.Payments;
using Storefront.Infrastructure;

namespace Storefront.Api.Setup;

/// <summary>What the API needs at startup, in two steps: the services, then the request pipeline.</summary>
internal static class ApiSetup
{
    public static void AddApiServices(this WebApplicationBuilder builder)
    {
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        builder.Services.AddOpenApi();
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            // Model-binding errors (for example page=abc) never reach ApiExceptionHandler; give them the same stable code.
            if (context.ProblemDetails.Status == StatusCodes.Status400BadRequest)
            {
                context.ProblemDetails.Extensions.TryAdd("code", ApiProblems.ValidationCode);
            }
        });
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddControllers();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.AddAdminAuth();
        builder.Services.AddScoped<HealthService>();
        builder.Services.AddScoped<IServiceManager, ServiceManager>();
        builder.Services.AddApiCors(builder.Configuration);
        builder.AddCartCleanup();
        builder.AddOrders();
        builder.AddPayments();
        builder.AddEmailOutbox();
        builder.Services
            .AddOptions<SiteOptions>()
            .Bind(builder.Configuration.GetSection(SiteOptions.SectionName))
            .Validate(SiteOptions.IsValid, "Site:Url must be an http(s) address without a path, for example https://shop.example.")
            .ValidateOnStart();
    }

    /// <summary>The order matters: errors first, then CORS, the limiter, the CSRF check, and who you are before what you may do.</summary>
    public static void UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        // Turns the empty 401, 403 and 429 answers into ProblemDetails.
        app.UseStatusCodePages();
        app.UseCors();
        app.UseRateLimiter();
        app.UseCsrfHeaderCheck();
        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        app.MapControllers();
    }

    /// <summary>Only the listed origins, and only GET: the panel and the shop reach the API through their own addresses.</summary>
    private static void AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
            options.AddDefaultPolicy(policy => policy
                .WithOrigins(allowedOrigins)
                .WithMethods(HttpMethods.Get)
                .AllowAnyHeader()));
    }

    /// <summary>
    /// Placing and finding an order (decisions 0032 and 0036): the settings, checked at startup, the number generator, the
    /// count of wrong emails, and the limits of requests. The address in the emails is the one of the site (<c>Site:Url</c>)
    /// when it is set.
    /// </summary>
    private static void AddOrders(this WebApplicationBuilder builder)
    {
        var siteUrl = builder.Configuration.GetSection(SiteOptions.SectionName).Get<SiteOptions>()?.Url ?? string.Empty;
        builder.Services
            .AddOptions<OrderOptions>()
            .Bind(builder.Configuration.GetSection(OrderOptions.SectionName))
            .PostConfigure(options =>
            {
                if (siteUrl.Length > 0 && builder.Configuration[$"{OrderOptions.SectionName}:{nameof(OrderOptions.StorefrontUrl)}"] is null)
                {
                    options.StorefrontUrl = siteUrl;
                }
            })
            .Validate(
                OrderOptions.IsValid,
                "Checkout is not valid: TermsVersion must be 1 to 50 characters, StorefrontUrl an http(s) address, "
                + "LookupAttemptsPerMinute and PlaceAttemptsPerMinute 1 to 1000, LookupFailuresPerOrder 1 to 100 and LookupFailureWindowMinutes 1 to 1440.")
            .ValidateOnStart();
        builder.Services.AddSingleton(services => services.GetRequiredService<IOptions<OrderOptions>>().Value);
        builder.Services.AddSingleton<IOrderNumberGenerator, RandomOrderNumberGenerator>();
        builder.Services.AddSingleton(services => new OrderLookupThrottle(
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<OrderOptions>()));
        builder.Services.AddOrderRateLimits();
    }

    /// <summary>
    /// Limits per minute for the open addresses of orders (debt D64). Finding an order is counted by address; placing one by
    /// the cart of the buyer, because behind the proxy every request has the same address (decisions 0014 and 0027).
    /// </summary>
    private static void AddOrderRateLimits(this IServiceCollection services) =>
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(OrderRateLimits.Lookup, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => Window(context.RequestServices.GetRequiredService<OrderOptions>().LookupAttemptsPerMinute)));
            limiter.AddPolicy(OrderRateLimits.Place, context => RateLimitPartition.GetFixedWindowLimiter(
                CartCookie.Read(context.Request) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => Window(context.RequestServices.GetRequiredService<OrderOptions>().PlaceAttemptsPerMinute)));
        });

    private static FixedWindowRateLimiterOptions Window(int permits) =>
        new() { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 };

    /// <summary>
    /// Paying an order (decision 0035): the settings, checked at startup, and the guard that keeps the test provider out of
    /// production. Payment turned off (<c>Payment:Enabled</c>) skips the guard, like the administrator login (decision 0013).
    /// </summary>
    private static void AddPayments(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(PaymentOptions.SectionName);
        builder.Services
            .AddOptions<PaymentOptions>()
            .Bind(section)
            .Validate(
                PaymentOptions.IsValid,
                "Payment:WindowMinutes must be between 1 minute and 7 days, Payment:GraceMinutes between 0 and 24 hours, "
                + "Payment:ExpiryIntervalMinutes between 1 minute and 24 hours, Payment:ExpiryInitialDelaySeconds between 0 and 3600 and Payment:ExpiryBatchSize between 1 and 1000.")
            .ValidateOnStart();
        builder.Services.AddSingleton(services => services.GetRequiredService<IOptions<PaymentOptions>>().Value);

        builder.Services.AddScoped<IPaymentExpiryService>(services => new PaymentExpiryService(
            services.GetRequiredService<IRepositoryManager>(),
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<PaymentOptions>()));

        if (section.Get<PaymentOptions>()?.Enabled ?? true)
        {
            PaymentTestEnvironments.EnsureAllowed(builder.Environment.EnvironmentName);
            // With payment off nobody can pay, so no order has a deadline and there is nothing to cancel.
            builder.Services.AddHostedService<PaymentExpiryWorker>();
        }
    }

    /// <summary>The queue of outgoing emails (decision 0036): its settings, checked at startup, and the worker that sends them.</summary>
    private static void AddEmailOutbox(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<EmailOutboxOptions>()
            .Bind(builder.Configuration.GetSection(EmailOutboxOptions.SectionName))
            .Validate(
                EmailOutboxOptions.IsValid,
                "EmailOutbox values are out of range: IntervalSeconds 1 to 3600, InitialDelaySeconds 0 to 3600, BatchSize 1 to 500, "
                + "MaxAttempts 1 to 20, RetryBaseSeconds 1 to 86400, LeaseSeconds 1 to 3600 and RetentionDays 1 to 3650.")
            .ValidateOnStart();
        builder.Services.AddSingleton(services => services.GetRequiredService<IOptions<EmailOutboxOptions>>().Value);
        builder.Services.AddScoped<IEmailOutboxService>(services => new EmailOutboxService(
            services.GetRequiredService<IRepositoryManager>(),
            services.GetRequiredService<IEmailSender>(),
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<EmailOutboxOptions>()));
        builder.Services.AddHostedService<EmailOutboxWorker>();
    }

    /// <summary>The daily cleanup of old carts (decision 0027), with its settings checked at startup.</summary>
    private static void AddCartCleanup(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<CartCleanupOptions>()
            .Bind(builder.Configuration.GetSection(CartCleanupOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddScoped<ICartCleanupService>(services => new CartCleanupService(
            services.GetRequiredService<IRepositoryManager>(),
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<IOptions<CartCleanupOptions>>().Value.BatchSize));
        builder.Services.AddHostedService<CartCleanupWorker>();
    }
}
