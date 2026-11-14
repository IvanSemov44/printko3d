using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Api.Auth;
using Storefront.Api.Cart;
using Storefront.Application.Catalog;
using Storefront.Application.Customers;
using Storefront.Application.Orders;
using Storefront.Infrastructure.Identity;

namespace Storefront.Api.Controllers;

/// <summary>The providers that are turned on, as the sign-in page and the profile read them.</summary>
public sealed record ExternalProvidersDto(IReadOnlyList<string> Providers);

/// <summary>
/// Sign-in and connecting through Google and Facebook (decision 0039). The browser goes to a start address and away to the provider; the
/// provider sends it back to the shop (`/signin-google`, `/signin-facebook`), which ends at the callback here and then at a page of the shop.
/// The shop is always the place to land, never an address that the request names.
/// </summary>
[ApiController]
[Route("api/v1/account/external")]
public sealed class ExternalLoginController(
    ICustomerExternalLoginService external,
    IServiceManager services,
    EnabledExternalProviders enabled,
    ExternalLinkIntent intent,
    OrderOptions shop) : ControllerBase
{
    private const string AuthSchemeKey = ".AuthScheme";

    private const string EmailVerifiedClaim = "email_verified";

    /// <summary>The providers that are turned on in this environment. Empty when no keys are set.</summary>
    [HttpGet("providers")]
    [AllowAnonymous]
    [ProducesResponseType<ExternalProvidersDto>(StatusCodes.Status200OK)]
    public ActionResult<ExternalProvidersDto> Providers() => Ok(new ExternalProvidersDto(enabled.Names));

    /// <summary>
    /// The signed-in buyer asks to connect a provider. It sets a short cookie that the start and the end of the connecting need, so
    /// that another site cannot connect its own account of the provider to the buyer's.
    /// </summary>
    /// <remarks>Errors: 401 when there is no session, 403 csrf.header_required, 404 when the provider is not turned on.</remarks>
    [HttpPost("{provider}/link")]
    [Authorize(AuthenticationSchemes = CustomerAuthentication.Scheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult Link(string provider)
    {
        if (enabled.Find(provider) is not { } name || CustomerSession.IdOf(User) is not { } customerId)
        {
            return NotFound();
        }

        intent.Issue(Response, customerId, name);
        return NoContent();
    }

    /// <summary>
    /// Sends the browser to the provider. With <c>mode=link</c> the signed-in buyer connects the provider to their account (it needs the
    /// cookie of `POST .../link`); otherwise the buyer signs in or registers. A provider that is not turned on is 404.
    /// </summary>
    [HttpGet("{provider}/start")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Start(string provider, [FromQuery] string? mode = null)
    {
        if (enabled.Find(provider) is not { } name)
        {
            return NotFound();
        }

        var linking = string.Equals(mode, "link", StringComparison.OrdinalIgnoreCase);
        if (linking && !(await CustomerSession.IdAsync(HttpContext) is { } customerId && intent.IsValid(Request, customerId, name)))
        {
            return LoginError("external_failed");
        }

        var callback = $"/api/v1/account/external/callback?mode={(linking ? "link" : "signin")}";
        return Challenge(new AuthenticationProperties { RedirectUri = callback }, name);
    }

    /// <summary>The end of the sign-in: reads what the provider said, signs in or connects, and sends the browser to a page of the shop.</summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<ActionResult> Callback([FromQuery] string? mode, CancellationToken cancellationToken)
    {
        var result = await HttpContext.AuthenticateAsync(CustomerAuthentication.ExternalScheme);
        // What the provider said is used once.
        await HttpContext.SignOutAsync(CustomerAuthentication.ExternalScheme);
        if (!result.Succeeded
            || result.Properties?.Items.TryGetValue(AuthSchemeKey, out var scheme) != true
            || enabled.Find(scheme ?? string.Empty) is not { } provider
            || result.Principal.FindFirstValue(ClaimTypes.NameIdentifier) is not { Length: > 0 } key)
        {
            return LoginError("external_failed");
        }

        var linking = string.Equals(mode, "link", StringComparison.OrdinalIgnoreCase);
        Guid? linkTo = null;
        if (linking)
        {
            linkTo = await CustomerSession.IdAsync(HttpContext);
            var proven = linkTo is { } id && intent.IsValid(Request, id, provider);
            ExternalLinkIntent.Clear(Response);
            if (!proven)
            {
                return LoginError("external_failed");
            }
        }

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        var identity = new ExternalIdentity(
            provider,
            key,
            email,
            IsVerified(provider, email, result.Principal),
            result.Principal.FindFirstValue(ClaimTypes.Name));

        var outcome = await external.SignInAsync(identity, linkTo, cancellationToken);
        switch (outcome.Outcome)
        {
            case ExternalSignInOutcome.SignedIn when outcome.Customer is { } signedIn:
                // One cart for the account: the one of this browser and the one of the account are joined, as after a sign-in with a password.
                var cart = await services.Cart.MergeIntoAccountAsync(CartCookie.Read(Request), signedIn.Id, cancellationToken);
                if (cart.Token is not null)
                {
                    CartCookie.Write(Response, cart.Token);
                }

                return Redirect(ShopAddress("/account"));
            case ExternalSignInOutcome.Linked:
                return Redirect(ShopAddress($"/account?linked={Uri.EscapeDataString(provider)}"));
            case ExternalSignInOutcome.AccountExists:
                return LoginError("account_exists");
            case ExternalSignInOutcome.EmailNotVerified:
                return LoginError("email_not_verified");
            default:
                return linking ? Redirect(ShopAddress("/account?error=already_linked")) : LoginError("already_linked");
        }
    }

    /// <summary>
    /// Google says whether it verified the address; Facebook gives an address only when it is a confirmed one (its documentation), so an
    /// address from Facebook counts as verified.
    /// </summary>
    private static bool IsVerified(string provider, string? email, ClaimsPrincipal principal) =>
        !string.IsNullOrWhiteSpace(email)
        && (provider == ExternalLoginOptions.FacebookName
            || string.Equals(principal.FindFirstValue(EmailVerifiedClaim), "true", StringComparison.OrdinalIgnoreCase));

    private RedirectResult LoginError(string code) => Redirect(ShopAddress($"/account/login?error={code}"));

    private string ShopAddress(string path) => $"{shop.StorefrontUrl.TrimEnd('/')}{path}";
}
