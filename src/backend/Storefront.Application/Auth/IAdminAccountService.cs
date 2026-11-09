namespace Storefront.Application.Auth;

public enum EnsureAdminResult
{
    Created,
    AlreadyExists,
}

/// <summary>Creating the administrator. There is no registration: this is used only by the create-admin command.</summary>
public interface IAdminAccountService
{
    /// <summary>
    /// Makes sure an administrator with this email exists. An existing account keeps its password.
    /// Throws <see cref="AdminAccountException"/> when the email or the password is not acceptable.
    /// </summary>
    Task<EnsureAdminResult> EnsureAdminAsync(string email, string password, CancellationToken cancellationToken);
}
