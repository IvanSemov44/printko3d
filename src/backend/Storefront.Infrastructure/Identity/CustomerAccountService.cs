using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Storefront.Application.Contracts;
using Storefront.Application.Customers;
using Storefront.Application.Exceptions;
using Storefront.Application.Orders;

namespace Storefront.Infrastructure.Identity;

internal sealed class CustomerAccountService(
    UserManager<CustomerUser> users,
    SignInManager<CustomerUser> signInManager,
    IRepositoryManager repositories,
    OrderOptions shop,
    CustomerOptions options,
    TimeProvider timeProvider,
    ILogger<CustomerAccountService> logger) : ICustomerAccountService
{
    public async Task RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var address = email.Trim();
        if (await users.FindByEmailAsync(address) is { } existing)
        {
            // The same answer as for a new address. Hash anyway, so the time does not tell them apart. The owner of the
            // address is told by email, so the page does not have to.
            users.PasswordHasher.HashPassword(new CustomerUser(), password);
            logger.LogInformation("Customer registration for an address that already has an account.");
            repositories.OutgoingEmails.Enqueue(
                CustomerEmails.AlreadyRegistered(existing.Email!, existing.DisplayName, shop.StorefrontUrl));
            await repositories.SaveAsync(cancellationToken);
            return;
        }

        var user = new CustomerUser
        {
            Id = Guid.NewGuid(),
            UserName = address,
            Email = address,
            DisplayName = displayName.Trim(),
            CreatedAt = timeProvider.GetUtcNow(),
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw ToValidationFailure(result);
        }

        await QueueConfirmationAsync(user, cancellationToken);
    }

    public async Task ConfirmEmailAsync(string email, string token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            throw TokenInvalid();
        }

        if (!(await users.ConfirmEmailAsync(user, token)).Succeeded)
        {
            throw TokenInvalid();
        }
    }

    public async Task ResendConfirmationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await CurrentAsync() ?? throw new InvalidCredentialsException();
        if (!user.EmailConfirmed)
        {
            await QueueConfirmationAsync(user, cancellationToken);
        }
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            logger.LogInformation("Password reset asked for an address with no account.");
            return;
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        repositories.OutgoingEmails.Enqueue(
            CustomerEmails.PasswordReset(user.Email!, user.DisplayName, shop.StorefrontUrl, token, options.TokenLifetimeMinutes));
        await repositories.SaveAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            users.PasswordHasher.HashPassword(new CustomerUser(), newPassword);
            throw TokenInvalid();
        }

        var result = await users.ResetPasswordAsync(user, token, newPassword);
        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.InvalidToken)))
        {
            throw TokenInvalid();
        }

        if (!result.Succeeded)
        {
            throw ToValidationFailure(result);
        }

        // The buyer read the email, so the address is theirs; a lockout from guessing no longer holds them out.
        // The token of the link is made from the security stamp, which the new password has just renewed: it works once.
        user.EmailConfirmed = true;
        await users.UpdateAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
    }

    public async Task<CustomerDto?> SignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            // Hash anyway, so an unknown email takes about as long as a wrong password.
            users.PasswordHasher.HashPassword(new CustomerUser(), password);
            logger.LogWarning("Customer sign-in failed: unknown account.");
            return null;
        }

        signInManager.AuthenticationScheme = CustomerAuthentication.Scheme;
        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            // No email in the log, and the caller cannot tell a lockout from a wrong password.
            logger.LogWarning(
                "Customer sign-in failed: {Reason}.",
                result.IsLockedOut ? "account locked out" : "wrong password");
            return null;
        }

        return await ToDtoAsync(user);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await CurrentAsync();
        await signInManager.Context.SignOutAsync(CustomerAuthentication.Scheme);
        if (user is not null)
        {
            // A new security stamp makes every cookie issued before now invalid on the server.
            await users.UpdateSecurityStampAsync(user);
        }
    }

    public async Task<CustomerDto?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await CurrentAsync();
        return user is null ? null : await ToDtoAsync(user);
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await CurrentAsync() ?? throw new InvalidCredentialsException();
        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
        {
            throw new RuleViolationException("The current password is wrong.", CustomerErrorCodes.CurrentPasswordWrong);
        }

        if (!result.Succeeded)
        {
            throw ToValidationFailure(result);
        }

        // The new password renews the security stamp, which ends every session, this one too: sign this one in again.
        signInManager.AuthenticationScheme = CustomerAuthentication.Scheme;
        await signInManager.SignInAsync(user, isPersistent: true);
    }

    private async Task QueueConfirmationAsync(CustomerUser user, CancellationToken cancellationToken)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        repositories.OutgoingEmails.Enqueue(
            CustomerEmails.Confirmation(user.Email!, user.DisplayName, shop.StorefrontUrl, token, options.TokenLifetimeMinutes));
        await repositories.SaveAsync(cancellationToken);
    }

    private static RuleViolationException TokenInvalid() =>
        new("The link is wrong, was already used or has expired.", CustomerErrorCodes.TokenInvalid);

    private Task<CustomerUser?> CurrentAsync() => users.GetUserAsync(signInManager.Context.User);

    private async Task<CustomerDto> ToDtoAsync(CustomerUser user) =>
        new(user.Email!, user.DisplayName, user.EmailConfirmed, await users.HasPasswordAsync(user));

    private static ValidationFailedException ToValidationFailure(IdentityResult result) =>
        new(
            "The account data is not allowed.",
            new Dictionary<string, string[]> { ["password"] = result.Errors.Select(error => error.Code).ToArray() });
}
