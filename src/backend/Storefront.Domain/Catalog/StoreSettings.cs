namespace Storefront.Domain.Catalog;

public sealed class StoreSettings : Entity
{
    public static readonly Guid SingletonId = new("0195a000-0000-7000-8000-000000000001");

    private StoreSettings()
    {
    }

    public StoreSettings(
        string storeName,
        string defaultCurrency,
        string defaultLocale,
        string contactEmail,
        string contactPhone,
        string primaryColor,
        string? logoKey = null,
        bool isDemo = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultLocale);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactPhone);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryColor);

        Id = SingletonId;
        StoreName = storeName;
        DefaultCurrency = defaultCurrency;
        DefaultLocale = defaultLocale;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        PrimaryColor = primaryColor;
        LogoKey = logoKey;
        IsDemo = isDemo;
    }

    public string StoreName { get; private set; } = string.Empty;

    public string DefaultCurrency { get; private set; } = string.Empty;

    public string DefaultLocale { get; private set; } = string.Empty;

    public string ContactEmail { get; private set; } = string.Empty;

    public string ContactPhone { get; private set; } = string.Empty;

    public string? LogoKey { get; private set; }

    public string PrimaryColor { get; private set; } = string.Empty;

    public bool IsDemo { get; private set; }
}
