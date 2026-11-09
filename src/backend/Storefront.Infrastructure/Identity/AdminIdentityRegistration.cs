using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Application.Auth;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Identity;

public static class AdminIdentityRegistration
{
    public const int MinimumPasswordLength = 12;

    public const int MaxFailedAccessAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// ASP.NET Identity for the administrator (users and one role in the database, the standard password hasher,
    /// lockout) and the data protection keys in the database, so a restart does not end every session.
    /// The host adds the cookie setup (see the Api project).
    /// </summary>
    public static IServiceCollection AddAdminIdentity(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddDataProtection()
            .SetApplicationName("printko3d")
            .PersistKeysToDbContext<StorefrontDbContext>();
        services.AddIdentityCore<AdminUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = MinimumPasswordLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<StorefrontDbContext>();
        services.AddScoped<IAdminAccountService, AdminAccountService>();
        return services;
    }

    /// <summary>
    /// Signing in and out. Only for a web host that has authentication (the Api); the create-admin command
    /// needs no sign-in and has no authentication services.
    /// </summary>
    public static IServiceCollection AddAdminSignIn(this IServiceCollection services)
    {
        services.AddIdentityCore<AdminUser>().AddSignInManager();
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        return services;
    }
}
