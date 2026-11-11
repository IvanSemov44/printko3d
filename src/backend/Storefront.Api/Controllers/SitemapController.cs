using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Storefront.Api.Setup;
using Storefront.Application.Catalog;
using Storefront.Application.Exceptions;

namespace Storefront.Api.Controllers;

/// <summary>sitemap.xml for search engines. It is read by robots, not by the apps, so it is not part of the OpenAPI contract.</summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("sitemap.xml")]
public sealed class SitemapController(IServiceManager services, IOptions<SiteOptions> site) : ControllerBase
{
    /// <remarks>Errors: 404 sitemap.not_configured when the environment has no public address of the shop (Site__Url).</remarks>
    [HttpGet]
    [ProducesResponseType<string>(StatusCodes.Status200OK, "application/xml")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetSitemap(CancellationToken cancellationToken)
    {
        var siteUrl = site.Value.Url;
        if (siteUrl == string.Empty)
        {
            throw new NotFoundException("The public address of the shop is not configured.", "sitemap.not_configured");
        }

        Response.Headers.CacheControl = "public, max-age=3600";
        return Content(await services.Catalog.GetSitemapAsync(siteUrl, cancellationToken), "application/xml; charset=utf-8");
    }
}
