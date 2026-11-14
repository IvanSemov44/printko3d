namespace Storefront.Application.Customers;

/// <summary>
/// The account of a buyer (decision 0039): register, sign in with a password, sign out, the profile and a new password.
/// Implemented where ASP.NET Identity is, so <c>Application</c> does not depend on it.
/// </summary>
public interface ICustomerAccountService
{
    /// <summary>
    /// Makes an account. An address that already has one gives the same answer as a new one, so the call does not tell who is
    /// registered. It does not sign the buyer in. Errors: 400 validation.failed (the password is too short or not allowed).
    /// </summary>
    Task RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken);

    /// <summary>Signs in with the password. Null when it did not work, and does not say why (wrong email, wrong password and a locked account look the same).</summary>
    Task<CustomerDto?> SignInAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Ends the session on the server: the cookie of this session stops working, even if someone kept a copy.</summary>
    Task SignOutAsync(CancellationToken cancellationToken);

    /// <summary>The buyer of the current request, or null.</summary>
    Task<CustomerDto?> GetCurrentAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sets a new password, after the current one. The other sessions end. Errors: 400 validation.failed (the new password is not allowed),
    /// 422 account.current_password_wrong, 401 when nobody is signed in.
    /// </summary>
    Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken);
}
