using System.ComponentModel.DataAnnotations;
using Storefront.Application.Catalog;

namespace Storefront.Api.Requests;

/// <summary>The body of the request that replaces the editable settings of the shop.</summary>
/// <param name="Version">The version the form was loaded with. A different value means someone else saved first.</param>
public sealed record UpdateStoreSettingsRequest(
    [Required, StringLength(200)] string StoreName,
    [Required, StringLength(3)] string DefaultCurrency,
    [Required, StringLength(10)] string DefaultLocale,
    [Required, StringLength(320)] string ContactEmail,
    [StringLength(50)] string? ContactPhone,
    [Required, StringLength(20)] string PrimaryColor,
    bool IsDemo,
    uint Version)
{
    public UpdateStoreSettings ToModel() =>
        new(StoreName, DefaultCurrency, DefaultLocale, ContactEmail, ContactPhone ?? string.Empty, PrimaryColor, IsDemo, Version);
}
