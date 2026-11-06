using Storefront.Application.Exceptions;

namespace Storefront.Application.Catalog;

internal static class Guard
{
    public static void NotBlank(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationFailedException($"{name} is required.");
        }
    }

    public static void NotNegative(long? value, string name)
    {
        if (value < 0)
        {
            throw new ValidationFailedException($"{name} must not be negative.");
        }
    }
}
