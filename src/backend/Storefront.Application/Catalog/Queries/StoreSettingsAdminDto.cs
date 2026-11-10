namespace Storefront.Application.Catalog.Queries;

/// <summary>The settings as the admin panel sees them: the public fields plus the row version.</summary>
public sealed record StoreSettingsAdminDto(
    string StoreName,
    string DefaultCurrency,
    string DefaultLocale,
    string ContactEmail,
    string ContactPhone,
    string? LogoKey,
    string PrimaryColor,
    bool IsDemo,
    uint Version);
