using System.ComponentModel.DataAnnotations;

namespace Storefront.Api.Setup;

/// <summary>Settings of the daily cart cleanup (decision 0027). The defaults are the values of the decision.</summary>
public sealed class CartCleanupOptions
{
    public const string SectionName = "CartCleanup";

    /// <summary>Hours between two runs.</summary>
    [Range(1, 168)]
    public int IntervalHours { get; set; } = 24;

    /// <summary>Seconds to wait after the start before the first run, so the start of the API is not slowed down.</summary>
    [Range(0, 3600)]
    public int InitialDelaySeconds { get; set; } = 60;

    /// <summary>Carts deleted by one statement. Small batches keep every transaction short.</summary>
    [Range(10, 5000)]
    public int BatchSize { get; set; } = 500;
}
