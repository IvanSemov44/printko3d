namespace Storefront.Application.Seeding;

/// <summary>The environments where demo data may be loaded. Fixed in code on purpose, not a setting.</summary>
public static class SeedEnvironments
{
    public const string Development = "Development";

    public const string Demo = "Demo";

    public static bool IsAllowed(string? environmentName) =>
        string.Equals(environmentName, Development, StringComparison.OrdinalIgnoreCase)
        || string.Equals(environmentName, Demo, StringComparison.OrdinalIgnoreCase);

    public static void EnsureAllowed(string? environmentName)
    {
        if (!IsAllowed(environmentName))
        {
            throw new SeedRefusedException(
                $"Seeding is only allowed in the '{Development}' and '{Demo}' environments, "
                + $"but the environment is '{environmentName}'.");
        }
    }
}
