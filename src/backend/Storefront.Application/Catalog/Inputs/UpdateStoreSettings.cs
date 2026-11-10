namespace Storefront.Application.Catalog;

/// <summary>Everything the settings form can change. <see cref="Version"/> is the value the form was loaded with.</summary>
public sealed record UpdateStoreSettings(
    string StoreName,
    string DefaultCurrency,
    string DefaultLocale,
    string ContactEmail,
    string ContactPhone,
    string PrimaryColor,
    bool IsDemo,
    uint Version);
