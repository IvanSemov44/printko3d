using System.Text.RegularExpressions;
using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

internal sealed partial class StoreSettingsService(
    IRepositoryManager repositories,
    IReadOnlyDictionary<string, int>? currencies = null) : IStoreSettingsService
{
    private readonly IReadOnlyDictionary<string, int> _currencies = currencies ?? StoreSettingsRules.DefaultCurrencies;

    public async Task<bool> EnsureAsync(NewStoreSettings input, CancellationToken cancellationToken)
    {
        Guard.NotBlank(input.StoreName, nameof(input.StoreName));
        Guard.NotBlank(input.DefaultLocale, nameof(input.DefaultLocale));
        Guard.NotBlank(input.ContactEmail, nameof(input.ContactEmail));
        Guard.NotBlank(input.DefaultCurrency, nameof(input.DefaultCurrency));
        Guard.NotBlank(input.PrimaryColor, nameof(input.PrimaryColor));
        if (!CurrencyPattern().IsMatch(input.DefaultCurrency))
        {
            throw new ValidationFailedException("The currency must be a three-letter upper-case code.");
        }

        if (!ColorPattern().IsMatch(input.PrimaryColor))
        {
            throw new ValidationFailedException("The primary color must look like #RRGGBB.");
        }

        if (!input.ContactEmail.Contains('@', StringComparison.Ordinal))
        {
            throw new ValidationFailedException("The contact email is not valid.");
        }

        if (await repositories.StoreSettings.GetAsync(trackChanges: false, cancellationToken) is not null)
        {
            return false;
        }

        repositories.StoreSettings.Create(new StoreSettings
        {
            StoreName = input.StoreName,
            DefaultCurrency = input.DefaultCurrency,
            DefaultLocale = input.DefaultLocale,
            ContactEmail = input.ContactEmail,
            ContactPhone = input.ContactPhone,
            LogoKey = input.LogoKey,
            PrimaryColor = input.PrimaryColor,
            IsDemo = input.IsDemo,
        });
        await repositories.SaveAsync(cancellationToken);
        return true;
    }

    public async Task<StoreSettingsAdminDto> GetAdminAsync(CancellationToken cancellationToken) =>
        ToDto(await FindAsync(trackChanges: false, cancellationToken));

    public async Task<StoreSettingsAdminDto> UpdateAsync(UpdateStoreSettings input, CancellationToken cancellationToken)
    {
        var color = Validate(input);
        var settings = await FindAsync(trackChanges: true, cancellationToken);
        repositories.StoreSettings.ExpectVersion(settings, input.Version);

        if (input.DefaultCurrency != settings.DefaultCurrency)
        {
            await EnsureCurrencyCanChangeAsync(settings.DefaultCurrency, input.DefaultCurrency, cancellationToken);
        }

        settings.StoreName = input.StoreName.Trim();
        settings.DefaultCurrency = input.DefaultCurrency;
        settings.DefaultLocale = input.DefaultLocale;
        settings.ContactEmail = input.ContactEmail;
        settings.ContactPhone = input.ContactPhone.Trim();
        settings.PrimaryColor = color;
        settings.IsDemo = input.IsDemo;
        await repositories.SaveAsync(cancellationToken);
        return ToDto(settings);
    }

    private async Task<StoreSettings> FindAsync(bool trackChanges, CancellationToken cancellationToken) =>
        await repositories.StoreSettings.GetAsync(trackChanges, cancellationToken)
            ?? throw new NotFoundException("Store settings were not found.", "store_settings.not_found");

    private static StoreSettingsAdminDto ToDto(StoreSettings s) => new(
        s.StoreName,
        s.DefaultCurrency,
        s.DefaultLocale,
        s.ContactEmail,
        s.ContactPhone,
        s.LogoKey,
        s.PrimaryColor,
        s.IsDemo,
        s.Version);

    // Prices have no currency of their own. Changing to a currency with another number of decimals would change
    // what every stored price means (1999 is 19.99 in euro and 1999 in yen), so it is refused while variants exist.
    private async Task EnsureCurrencyCanChangeAsync(string from, string to, CancellationToken cancellationToken)
    {
        var sameDigits = _currencies.TryGetValue(from, out var fromDigits)
            && _currencies.TryGetValue(to, out var toDigits)
            && fromDigits == toDigits;
        if (!sameDigits && await repositories.Product.AnyVariantsAsync(cancellationToken))
        {
            throw new ConflictException(
                "The shop has prices, and the new currency has another number of decimals.",
                "store_settings.currency_locked");
        }
    }

    /// <summary>Checks every field and returns the primary colour in lower case.</summary>
    private string Validate(UpdateStoreSettings input)
    {
        var errors = new FieldErrors();
        if (string.IsNullOrWhiteSpace(input.StoreName))
        {
            errors.Add("storeName", "Name is required.");
        }
        else if (input.StoreName.Trim().Length > StoreSettingsRules.NameMaxLength)
        {
            errors.Add("storeName", $"Name must be at most {StoreSettingsRules.NameMaxLength} characters.");
        }

        if (!_currencies.ContainsKey(input.DefaultCurrency ?? string.Empty))
        {
            errors.Add("defaultCurrency", "The currency is not allowed.");
        }

        if (!StoreSettingsRules.Locales.Contains(input.DefaultLocale ?? string.Empty))
        {
            errors.Add("defaultLocale", "The language has no translations.");
        }

        if (string.IsNullOrEmpty(input.ContactEmail))
        {
            errors.Add("contactEmail", "Email is required.");
        }
        else if (input.ContactEmail.Length > StoreSettingsRules.EmailMaxLength
            || !StoreSettingsRules.IsEmail(input.ContactEmail))
        {
            errors.Add("contactEmail", "The email is not valid.");
        }

        var phone = (input.ContactPhone ?? string.Empty).Trim();
        if (phone.Length > StoreSettingsRules.PhoneMaxLength || (phone.Length > 0 && !StoreSettingsRules.IsPhone(phone)))
        {
            errors.Add("contactPhone", "The phone is not valid.");
        }

        var color = (input.PrimaryColor ?? string.Empty).ToLowerInvariant();
        if (!StoreSettingsRules.IsHexColor(color))
        {
            errors.Add("primaryColor", "The primary color must look like #RRGGBB.");
        }
        else if (ColorContrast.Ratio(color, StoreSettingsRules.White) < StoreSettingsRules.MinColorContrast)
        {
            errors.Add("primaryColor", "The primary color is too light: it needs a contrast of at least 3:1 with white.");
        }

        errors.ThrowIfAny("The store settings are not valid.");
        return color;
    }

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorPattern();
}
