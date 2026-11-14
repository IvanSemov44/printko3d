using Storefront.Application.Orders;

namespace Storefront.Api.Auth;

/// <summary>
/// The address of the shop for the social sign-in. The shop reaches the API through its own proxy, so the API sees its internal
/// address as the host. The provider must send the buyer back to the address of the shop, and the keys at the provider hold exactly
/// that address. So for the addresses of the sign-in the scheme and the host are the ones of <c>Site:Url</c> (read by
/// <see cref="OrderOptions.StorefrontUrl"/>), and never what the request says about itself.
/// </summary>
internal static class ExternalLoginAddress
{
    public const string StartPrefix = "/api/v1/account/external";

    public const string CallbackPrefix = "/signin-";

    public static IApplicationBuilder UseExternalLoginAddress(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (IsExternalLoginPath(context.Request.Path))
            {
                var shop = new Uri(context.RequestServices.GetRequiredService<OrderOptions>().StorefrontUrl);
                context.Request.Scheme = shop.Scheme;
                // Without the default port: the address that the provider holds has none, and a ":443" would not match it.
                context.Request.Host = shop.IsDefaultPort ? new HostString(shop.Host) : new HostString(shop.Host, shop.Port);
            }

            await next(context);
        });

    private static bool IsExternalLoginPath(PathString path) =>
        path.StartsWithSegments(StartPrefix, StringComparison.OrdinalIgnoreCase)
        || path.Value?.StartsWith(CallbackPrefix, StringComparison.OrdinalIgnoreCase) == true;
}
