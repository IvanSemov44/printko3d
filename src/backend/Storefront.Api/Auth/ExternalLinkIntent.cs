using Microsoft.AspNetCore.DataProtection;

namespace Storefront.Api.Auth;

/// <summary>
/// The proof that the buyer asked to connect a provider (decision 0039). Connecting starts with a navigation of the browser, which cannot
/// carry the CSRF header, so another site could send the buyer there with its own Google account and connect that to the buyer's account.
/// So the page first asks with a request that does carry the header (`POST .../link`), and gets a short cookie that only that request
/// can set. The start and the end of the sign-in both need it, for the same account and the same provider.
/// </summary>
public sealed class ExternalLinkIntent(IDataProtectionProvider protection, TimeProvider time)
{
    public const string CookieName = "__Host-printko-link-intent";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly ITimeLimitedDataProtector _protector =
        protection.CreateProtector("printko.customer.link-intent").ToTimeLimitedDataProtector();

    public void Issue(HttpResponse response, Guid customerId, string provider) =>
        response.Cookies.Append(CookieName, _protector.Protect($"{customerId:N}|{provider}", time.GetUtcNow().Add(Lifetime)), Options());

    /// <summary>True when the request carries an intent, still valid, for this buyer and this provider.</summary>
    public bool IsValid(HttpRequest request, Guid customerId, string provider)
    {
        if (request.Cookies[CookieName] is not { Length: > 0 } cookie)
        {
            return false;
        }

        try
        {
            return _protector.Unprotect(cookie) == $"{customerId:N}|{provider}";
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }

    public static void Clear(HttpResponse response) => response.Cookies.Delete(CookieName, Options());

    private static CookieOptions Options() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        IsEssential = true,
    };
}
