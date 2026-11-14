namespace Storefront.Application.Customers;

/// <summary>Settings of the customer accounts (decision 0039), section <c>Customer</c>.</summary>
public sealed class CustomerOptions
{
    public const string SectionName = "Customer";

    /// <summary>How long the link in a confirmation or password email works, in minutes.</summary>
    public int TokenLifetimeMinutes { get; set; } = 120;

    /// <summary>
    /// Limit of requests per minute to the open account addresses (sign-in, register, forgot password), counted by address.
    /// Behind the proxy of the shop every request has the same address, so the limit is generous: the lockout of the account is
    /// the real guard against guessing (debt D65).
    /// </summary>
    public int AttemptsPerMinute { get; set; } = 60;

    /// <summary>How many days the sign-in lasts without a visit.</summary>
    public int SessionDays { get; set; } = 14;

    public static bool IsValid(CustomerOptions options) =>
        options.TokenLifetimeMinutes is >= 5 and <= 10_080
        && options.AttemptsPerMinute is >= 1 and <= 1000
        && options.SessionDays is >= 1 and <= 90;
}
