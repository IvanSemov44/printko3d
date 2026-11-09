namespace Storefront.Api;

/// <summary>Settings for the administrator login (section "AdminLogin").</summary>
public sealed class AdminLoginOptions
{
    public const string SectionName = "AdminLogin";

    /// <summary>
    /// Whether the login endpoint works. On by default; while on, the API refuses to start outside
    /// Development and Demo (decision 0013). Turning it off keeps every closed address closed.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How many sign-in requests one client address may send in a minute.</summary>
    public int LoginAttemptsPerMinute { get; set; } = 5;
}
