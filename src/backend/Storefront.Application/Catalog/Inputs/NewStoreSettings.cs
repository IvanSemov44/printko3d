namespace Storefront.Application.Catalog;

public sealed record NewStoreSettings(
    string StoreName,
    string DefaultCurrency,
    string DefaultLocale,
    string ContactEmail,
    string ContactPhone,
    string PrimaryColor,
    bool IsDemo = false,
    string? LogoKey = null);
