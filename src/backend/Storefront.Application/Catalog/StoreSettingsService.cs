using System.Text.RegularExpressions;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

internal sealed partial class StoreSettingsService(IRepositoryManager repositories) : IStoreSettingsService
{
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

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorPattern();
}
