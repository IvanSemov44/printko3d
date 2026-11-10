namespace Storefront.Domain.Catalog;

public sealed class StoreSettings : Entity
{
    public static readonly Guid SingletonId = new("0195a000-0000-7000-8000-000000000001");

    public StoreSettings() => Id = SingletonId;

    public string StoreName { get; set; } = string.Empty;

    public string DefaultCurrency { get; set; } = string.Empty;

    public string DefaultLocale { get; set; } = string.Empty;

    public string ContactEmail { get; set; } = string.Empty;

    public string ContactPhone { get; set; } = string.Empty;

    public string? LogoKey { get; set; }

    public string PrimaryColor { get; set; } = string.Empty;

    public bool IsDemo { get; set; }

    /// <summary>PostgreSQL row version (xmin). Changes on every write; used to detect two administrators editing at once.</summary>
    public uint Version { get; set; }
}
