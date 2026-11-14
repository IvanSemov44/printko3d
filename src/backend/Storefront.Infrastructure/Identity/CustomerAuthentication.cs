namespace Storefront.Infrastructure.Identity;

/// <summary>The names of the customer's sign-in, separate from the administrator's (decision 0039).</summary>
public static class CustomerAuthentication
{
    public const string Scheme = "printko-customer";

    public const string CookieName = "__Host-printko-customer";

    /// <summary>The short-lived cookie that holds the answer of a social provider between the provider and the callback of the shop.</summary>
    public const string ExternalScheme = "printko-customer-external";

    public const string ExternalCookieName = "__Host-printko-customer-external";
}
