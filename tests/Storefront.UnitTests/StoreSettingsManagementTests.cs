using Storefront.Application.Catalog;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

public sealed class StoreSettingsManagementTests
{
    private readonly FakeRepositoryManager _repositories = new();

    private StoreSettingsService Service(IReadOnlyDictionary<string, int>? currencies = null) =>
        new(_repositories, currencies);

    private async Task<StoreSettingsService> ServiceWithRowAsync(
        IReadOnlyDictionary<string, int>? currencies = null, string currency = "EUR")
    {
        var service = Service(currencies);
        await service.EnsureAsync(
            new NewStoreSettings("Shop", currency, "bg", "shop@example.com", "000", "#112233", IsDemo: true),
            default);
        return service;
    }

    private uint CurrentVersion => _repositories.Settings.Single().Version;

    private UpdateStoreSettings Input(
        string name = "New shop",
        string currency = "EUR",
        string locale = "bg",
        string email = "info@example.com",
        string phone = "+359 (88) 123-456",
        string color = "#2f5d8a",
        bool isDemo = false,
        uint? version = null) =>
        new(name, currency, locale, email, phone, color, isDemo, version ?? 0);

    private async Task AssertRejectedAsync(UpdateStoreSettings input, string field)
    {
        var service = await ServiceWithRowAsync();
        var before = _repositories.Settings.Single().StoreName;

        var error = await Assert.ThrowsAsync<ValidationFailedException>(() => service.UpdateAsync(input with { Version = CurrentVersion }, default));

        Assert.Contains(field, error.Errors.Keys);
        Assert.Equal(before, _repositories.Settings.Single().StoreName);
    }

    private async Task AssertAcceptedAsync(UpdateStoreSettings input)
    {
        var service = await ServiceWithRowAsync();

        var result = await service.UpdateAsync(input with { Version = CurrentVersion }, default);

        Assert.NotNull(result);
    }

    // --- Reading ---

    [Fact]
    public async Task Get_ReturnsTheSettingsWithTheirVersion()
    {
        var service = await ServiceWithRowAsync();

        var settings = await service.GetAdminAsync(default);

        Assert.Equal("Shop", settings.StoreName);
        Assert.Equal(CurrentVersion, settings.Version);
    }

    [Fact]
    public async Task Get_WhenTheRowIsMissing_IsNotFound()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(() => Service().GetAdminAsync(default));

        Assert.Equal("store_settings.not_found", error.Code);
    }

    [Fact]
    public async Task Update_WhenTheRowIsMissing_IsNotFound_AndCreatesNothing()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(() => Service().UpdateAsync(Input(version: 1), default));

        Assert.Equal("store_settings.not_found", error.Code);
        Assert.Empty(_repositories.Settings);
    }

    // --- A good update ---

    [Fact]
    public async Task Update_ReplacesEveryField_TrimsTheNameAndLowercasesTheColor()
    {
        var service = await ServiceWithRowAsync();
        var before = CurrentVersion;

        var result = await service.UpdateAsync(
            Input(name: "  New shop  ", currency: "BGN", color: "#2F5D8A", isDemo: false, version: before), default);

        var row = _repositories.Settings.Single();
        Assert.Equal("New shop", row.StoreName);
        Assert.Equal("BGN", row.DefaultCurrency);
        Assert.Equal("info@example.com", row.ContactEmail);
        Assert.Equal("+359 (88) 123-456", row.ContactPhone);
        Assert.Equal("#2f5d8a", row.PrimaryColor);
        Assert.False(row.IsDemo);
        Assert.Equal(row.Version, result.Version);
        Assert.NotEqual(before, result.Version);
        Assert.Single(_repositories.Settings);
    }

    // --- Name ---

    [Fact]
    public async Task Name_OfExactlyTheLimit_IsAccepted() =>
        await AssertAcceptedAsync(Input(name: new string('a', 200)));

    [Fact]
    public async Task Name_OneOverTheLimit_IsRejected() =>
        await AssertRejectedAsync(Input(name: new string('a', 201)), "storeName");

    [Fact]
    public async Task Name_WithSpacesAroundTheLimit_IsMeasuredAfterTrimming() =>
        await AssertAcceptedAsync(Input(name: " " + new string('a', 200) + " "));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Name_Blank_IsRejected(string name) =>
        await AssertRejectedAsync(Input(name: name), "storeName");

    // --- Currency and language ---

    [Theory]
    [InlineData("eur")]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("USD")]
    [InlineData("")]
    public async Task Currency_ThatIsNotOnTheAllowedList_IsRejected(string currency) =>
        await AssertRejectedAsync(Input(currency: currency), "defaultCurrency");

    [Fact]
    public async Task Currency_OnTheAllowedList_IsAccepted() =>
        await AssertAcceptedAsync(Input(currency: "BGN"));

    [Theory]
    [InlineData("en")]
    [InlineData("BG")]
    [InlineData("")]
    public async Task Language_WithoutTranslations_IsRejected(string locale) =>
        await AssertRejectedAsync(Input(locale: locale), "defaultLocale");

    // --- Email ---

    [Theory]
    [InlineData("")]
    [InlineData("info")]
    [InlineData("info@")]
    [InlineData("info@example")]
    [InlineData("@example.com")]
    [InlineData("in fo@example.com")]
    [InlineData("info@exa mple.com")]
    [InlineData("info@@example.com")]
    [InlineData("info@example..com")]
    public async Task Email_WithABadFormat_IsRejected(string email) =>
        await AssertRejectedAsync(Input(email: email), "contactEmail");

    [Fact]
    public async Task Email_OfExactlyTheLimit_IsAccepted() =>
        await AssertAcceptedAsync(Input(email: new string('a', 315) + "@x.bg"));

    [Fact]
    public async Task Email_OneOverTheLimit_IsRejected() =>
        await AssertRejectedAsync(Input(email: new string('a', 316) + "@x.bg"), "contactEmail");

    // --- Phone ---

    [Fact]
    public async Task Phone_Empty_IsAccepted() =>
        await AssertAcceptedAsync(Input(phone: ""));

    [Fact]
    public async Task Phone_OfExactlyTheLimit_IsAccepted() =>
        await AssertAcceptedAsync(Input(phone: new string('1', 50)));

    [Fact]
    public async Task Phone_OneOverTheLimit_IsRejected() =>
        await AssertRejectedAsync(Input(phone: new string('1', 51)), "contactPhone");

    [Theory]
    [InlineData("call me")]
    [InlineData("0888 12x456")]
    [InlineData("0888/123456")]
    public async Task Phone_WithLettersOrOtherSigns_IsRejected(string phone) =>
        await AssertRejectedAsync(Input(phone: phone), "contactPhone");

    // --- Colour ---

    [Theory]
    [InlineData("#12345G")]
    [InlineData("#abc")]
    [InlineData("112233")]
    [InlineData("#1122334")]
    [InlineData("")]
    public async Task Color_WithABadFormat_IsRejected(string color) =>
        await AssertRejectedAsync(Input(color: color), "primaryColor");

    [Fact]
    public async Task Color_WithCapitalLetters_IsAcceptedAndStoredInLowerCase()
    {
        var service = await ServiceWithRowAsync();

        await service.UpdateAsync(Input(color: "#1A2B3C", version: CurrentVersion), default);

        Assert.Equal("#1a2b3c", _repositories.Settings.Single().PrimaryColor);
    }

    // #949494 is the lightest grey that still reaches 3:1 against white (3.03); #959595 is just under it (2.997).
    // The same colours are checked in packages/shared (color.test.ts), so the two formulas cannot drift apart.
    [Fact]
    public async Task Color_ExactlyOnTheContrastLimit_IsAccepted() =>
        await AssertAcceptedAsync(Input(color: "#949494"));

    [Fact]
    public async Task Color_JustUnderTheContrastLimit_IsRejected() =>
        await AssertRejectedAsync(Input(color: "#959595"), "primaryColor");

    [Fact]
    public async Task Color_White_IsRejected() =>
        await AssertRejectedAsync(Input(color: "#ffffff"), "primaryColor");

    [Fact]
    public async Task Color_Black_IsAccepted() =>
        await AssertAcceptedAsync(Input(color: "#000000"));

    [Theory]
    [InlineData("#ffffff", "#ffffff", 1.0)]
    [InlineData("#000000", "#ffffff", 21.0)]
    [InlineData("#949494", "#ffffff", 3.0303)]
    [InlineData("#2f5d8a", "#ffffff", 6.877)]
    [InlineData("#f25a18", "#ffffff", 3.359)]
    [InlineData("#ff6b2c", "#ffffff", 2.841)]
    public void ContrastRatio_MatchesTheValuesOfTheStorefrontFormula(string first, string second, double expected)
    {
        var ratio = ColorContrast.Ratio(first, second);

        Assert.Equal(expected, ratio, tolerance: 0.01);
    }

    // --- Several mistakes at once ---

    [Fact]
    public async Task Update_ReportsEveryBadFieldTogether()
    {
        var service = await ServiceWithRowAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(() => service.UpdateAsync(
            Input(name: "", email: "x", color: "#fff", version: CurrentVersion), default));

        Assert.Equal(["contactEmail", "primaryColor", "storeName"], error.Errors.Keys.Order().ToArray());
    }

    // --- Version ---

    [Fact]
    public async Task Update_WithAStaleVersion_IsAConflict_AndTheRowStaysAsItWas()
    {
        var service = await ServiceWithRowAsync();
        var stale = CurrentVersion;
        await service.UpdateAsync(Input(name: "First", version: stale), default);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAsync(Input(name: "Second", version: stale), default));

        Assert.Equal("store_settings.version_conflict", error.Code);
    }

    // --- Currency change and the prices ---

    private static readonly Dictionary<string, int> EuroAndYen = new() { ["EUR"] = 2, ["BGN"] = 2, ["JPY"] = 0 };

    private async Task AddVariantAsync()
    {
        var services = new ServiceManager(_repositories, new FakeImageStorage());
        var categoryId = await services.Category.CreateAsync(new NewCategory("Figures", "figures"), default);
        var productId = await services.Product.CreateAsync(
            new NewProduct(categoryId, "Dragon", "dragon", "Short", "Long", FulfilmentType.InStock), default);
        await services.Product.AddVariantAsync(productId, new NewVariant("SKU-1", "Blue", 1999, 10, StockQuantity: 5), default);
    }

    [Fact]
    public async Task CurrencyChange_ToOtherDecimals_IsLocked_WhenThereArePrices()
    {
        var service = await ServiceWithRowAsync(EuroAndYen);
        await AddVariantAsync();

        var error = await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(Input(currency: "JPY", version: CurrentVersion), default));

        Assert.Equal("store_settings.currency_locked", error.Code);
        Assert.Equal("EUR", _repositories.Settings.Single().DefaultCurrency);
    }

    [Fact]
    public async Task CurrencyChange_ToOtherDecimals_IsAllowed_WhenThereAreNoPrices()
    {
        var service = await ServiceWithRowAsync(EuroAndYen);

        await service.UpdateAsync(Input(currency: "JPY", version: CurrentVersion), default);

        Assert.Equal("JPY", _repositories.Settings.Single().DefaultCurrency);
    }

    [Fact]
    public async Task CurrencyChange_ToTheSameDecimals_IsAllowed_AndPricesAreNotTouched()
    {
        var service = await ServiceWithRowAsync(EuroAndYen);
        await AddVariantAsync();

        await service.UpdateAsync(Input(currency: "BGN", version: CurrentVersion), default);

        Assert.Equal("BGN", _repositories.Settings.Single().DefaultCurrency);
        Assert.Equal(1999, _repositories.Products.Single().Variants.Single().Price);
    }

    [Fact]
    public async Task SavingWithTheSameCurrency_IsNeverLocked_EvenWithPricesAndOtherDecimals()
    {
        var service = await ServiceWithRowAsync(EuroAndYen, currency: "JPY");
        await AddVariantAsync();

        await service.UpdateAsync(Input(currency: "JPY", name: "Renamed", version: CurrentVersion), default);

        Assert.Equal("Renamed", _repositories.Settings.Single().StoreName);
    }
}
