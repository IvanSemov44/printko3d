using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Storefront.Application.Auth;

namespace Storefront.Infrastructure.Identity;

internal sealed class AdminAuthService(
    UserManager<AdminUser> users,
    SignInManager<AdminUser> signInManager,
    ILogger<AdminAuthService> logger) : IAdminAuthService
{
    public async Task<AdminUserDto?> SignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            // Hash anyway, so an unknown email takes about as long as a wrong password.
            users.PasswordHasher.HashPassword(new AdminUser(), password);
            logger.LogWarning("Admin sign-in failed: unknown account.");
            return null;
        }

        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            // No email in the log, and the caller cannot tell a lockout from a wrong password.
            logger.LogWarning(
                "Admin sign-in failed: {Reason}.",
                result.IsLockedOut ? "account locked out" : "wrong password");
            return null;
        }

        return new AdminUserDto(user.Email!);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await users.GetUserAsync(signInManager.Context.User);
        await signInManager.SignOutAsync();
        if (user is not null)
        {
            // A new security stamp makes every cookie issued before now invalid on the server.
            await users.UpdateSecurityStampAsync(user);
        }
    }

    public async Task<AdminUserDto?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await users.GetUserAsync(signInManager.Context.User);
        return user is null ? null : new AdminUserDto(user.Email!);
    }
}
