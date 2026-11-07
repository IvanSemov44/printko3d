namespace Storefront.Application.Catalog.Queries;

public sealed record StoreSettingsDto(
    string StoreName,
    string DefaultCurrency,
    string DefaultLocale,
    string ContactEmail,
    string ContactPhone,
    string? LogoKey,
    string PrimaryColor,
    bool IsDemo);
