namespace Storefront.Application.Auth;

/// <summary>
/// The environments where the administrator may sign in with an email and a password only.
/// Fixed in code on purpose, not a setting (decision 0013): without a second factor, production must not start
/// with administrator login enabled. Remove or replace this check when two-factor protection is in place.
/// </summary>
public static class AdminLoginEnvironments
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
            throw new AdminLoginRefusedException(
                $"Administrator login without two-factor protection is only allowed in the '{Development}' "
                + $"and '{Demo}' environments, but the environment is '{environmentName}'. "
                + "Set AdminLogin:Enabled to false to start without administrator login (see decision 0013).");
        }
    }
}
