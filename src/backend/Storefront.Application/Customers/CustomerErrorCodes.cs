namespace Storefront.Application.Customers;

/// <summary>Stable codes of the customer account calls, for API clients (decision 0039).</summary>
public static class CustomerErrorCodes
{
    /// <summary>The link of an email is wrong, was already used or has expired.</summary>
    public const string TokenInvalid = "account.token_invalid";

    /// <summary>The current password given to change the password is wrong.</summary>
    public const string CurrentPasswordWrong = "account.current_password_wrong";
}
