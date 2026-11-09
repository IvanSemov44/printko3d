namespace Storefront.Application.Auth;

/// <summary>Signing the administrator in and out, and telling who is signed in on the current request.</summary>
public interface IAdminAuthService
{
    /// <summary>
    /// Signs in with an email and a password. Returns null when it did not work, and does not say why
    /// (wrong email, wrong password and a locked account look the same).
    /// </summary>
    Task<AdminUserDto?> SignInAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Ends the session on the server: the cookie of this session stops working, even if someone kept a copy.</summary>
    Task SignOutAsync(CancellationToken cancellationToken);

    /// <summary>The user of the current request, or null.</summary>
    Task<AdminUserDto?> GetCurrentAsync(CancellationToken cancellationToken);
}
