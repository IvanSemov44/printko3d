namespace Storefront.Application.Payments;

/// <summary>
/// The environments where the test payment may run. Fixed in code on purpose, not a setting (decision 0035): the test
/// provider marks an order paid on a click, so production must not start with it. Replace this check when a real
/// provider is chosen.
/// </summary>
public static class PaymentTestEnvironments
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
            throw new PaymentTestRefusedException(
                $"The test payment is only allowed in the '{Development}' and '{Demo}' environments, "
                + $"but the environment is '{environmentName}'. Choose a real payment provider (see decision 0035).");
        }
    }
}
