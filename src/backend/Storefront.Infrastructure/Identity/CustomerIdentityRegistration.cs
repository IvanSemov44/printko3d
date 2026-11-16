using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Storefront.Application.Customers;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Identity;

public static class CustomerIdentityRegistration
{
    /// <summary>
    /// ASP.NET Identity for the buyer (decision 0039): users in <c>customer_users</c>, no roles, the same password and lockout
    /// rules as the administrator, and the sign-in manager. Only for a web host that has authentication (the Api). The cookie
    /// scheme of the buyer is added by the host.
    /// </summary>
    /// <remarks>
    /// The sign-in manager is added by hand, not by <c>AddSignInManager</c>: that one also registers the one global
    /// <c>ISecurityStampValidator</c>, and the last to register wins, so it would check the administrator's cookie against the buyers.
    /// </remarks>
    public static IServiceCollection AddCustomerIdentity(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddIdentityCore<CustomerUser>(AdminIdentityRegistration.ApplyRules)
            .AddEntityFrameworkStores<StorefrontDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<SignInManager<CustomerUser>>();
        services.TryAddScoped<IUserConfirmation<CustomerUser>, DefaultUserConfirmation<CustomerUser>>();
        services.AddScoped<ICustomerAccountService, CustomerAccountService>();
        services.AddScoped<ICustomerExternalLoginService, CustomerExternalLoginService>();
        return services;
    }
}
