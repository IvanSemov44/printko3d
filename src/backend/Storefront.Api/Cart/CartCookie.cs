namespace Storefront.Api.Cart;

/// <summary>
/// The cart cookie (decision 0027). The value is the secret that recognises the shopper; the database keeps only its hash.
/// `__Host-` makes the browser require Secure, Path=/ and no Domain.
/// </summary>
internal static class CartCookie
{
    public const string Name = "__Host-printko-cart";

    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    /// <summary>Removes the cookie from the browser, when the buyer signs out: the next shopper of this browser starts with no cart.</summary>
    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, new CookieOptions { Secure = true, SameSite = SameSiteMode.Strict, Path = "/" });

    /// <summary>Sets the cookie, or renews it for another 30 days when the shopper already has it.</summary>
    public static void Write(HttpResponse response, string token) =>
        response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = Lifetime,
            IsEssential = true,
        });
}
