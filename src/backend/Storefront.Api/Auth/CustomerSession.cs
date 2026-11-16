using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Storefront.Infrastructure.Identity;

namespace Storefront.Api.Auth;

/// <summary>Who the buyer is on a request (decision 0039), by the buyer's own cookie and never the administrator's.</summary>
internal static class CustomerSession
{
    /// <summary>The account of the signed-in buyer for an address that is open to everyone, or null for a guest. The session is checked in full, security stamp included.</summary>
    public static async Task<Guid?> IdAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(CustomerAuthentication.Scheme);
        return result.Succeeded ? IdOf(result.Principal) : null;
    }

    /// <summary>The account of the buyer for an address that needs a sign-in, where the framework has already read the session.</summary>
    public static Guid? IdOf(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
