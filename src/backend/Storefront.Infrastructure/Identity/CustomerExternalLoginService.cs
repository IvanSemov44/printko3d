using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Storefront.Application.Contracts;
using Storefront.Application.Customers;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Identity;

internal sealed class CustomerExternalLoginService(
    StorefrontDbContext context,
    UserManager<CustomerUser> users,
    SignInManager<CustomerUser> signInManager,
    IRepositoryManager repositories,
    TimeProvider timeProvider,
    ILogger<CustomerExternalLoginService> logger) : ICustomerExternalLoginService
{
    private const int MaxNameLength = 200;

    public async Task<ExternalSignInResult> SignInAsync(ExternalIdentity identity, Guid? linkTo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await context.CustomerExternalLogins.AsNoTracking()
            .SingleOrDefaultAsync(l => l.Provider == identity.Provider && l.ProviderKey == identity.ProviderKey, cancellationToken);

        if (linkTo is { } target)
        {
            return await LinkAsync(identity, existing, target, cancellationToken);
        }

        if (existing is not null)
        {
            var linked = await users.FindByIdAsync(existing.CustomerId.ToString());
            return linked is null ? new ExternalSignInResult(ExternalSignInOutcome.EmailNotVerified) : await SignedInAsync(linked, cancellationToken);
        }

        if (!identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Email))
        {
            logger.LogWarning("Sign-in through {Provider} refused: no verified address.", identity.Provider);
            return new ExternalSignInResult(ExternalSignInOutcome.EmailNotVerified);
        }

        var address = identity.Email.Trim();
        var user = await users.FindByEmailAsync(address);
        if (user is null)
        {
            user = new CustomerUser
            {
                Id = Guid.NewGuid(),
                UserName = address,
                Email = address,
                EmailConfirmed = true,
                DisplayName = DisplayNameOf(identity, address),
                CreatedAt = timeProvider.GetUtcNow(),
            };
            if (!(await users.CreateAsync(user)).Succeeded)
            {
                return new ExternalSignInResult(ExternalSignInOutcome.EmailNotVerified);
            }
        }
        else if (user.EmailConfirmed)
        {
            logger.LogInformation("Sign-in through {Provider} refused: the address has an account of its own.", identity.Provider);
            return new ExternalSignInResult(ExternalSignInOutcome.AccountExists);
        }
        else
        {
            // The address was registered by someone who never confirmed it. The provider has: the address is theirs, and the
            // password that the first one chose is taken away, so it cannot be used to get into this account.
            user.EmailConfirmed = true;
            await users.UpdateAsync(user);
            if (await users.HasPasswordAsync(user))
            {
                await users.RemovePasswordAsync(user);
            }

            await users.UpdateSecurityStampAsync(user);
        }

        if (!await TryAddLinkAsync(user.Id, identity, cancellationToken))
        {
            return new ExternalSignInResult(ExternalSignInOutcome.AlreadyLinked);
        }

        return await SignedInAsync(user, cancellationToken);
    }

    private async Task<ExternalSignInResult> LinkAsync(
        ExternalIdentity identity, CustomerExternalLogin? existing, Guid target, CancellationToken cancellationToken)
    {
        if (existing is not null)
        {
            return new ExternalSignInResult(existing.CustomerId == target ? ExternalSignInOutcome.Linked : ExternalSignInOutcome.AlreadyLinked);
        }

        var alreadyHasProvider = await context.CustomerExternalLogins.AsNoTracking()
            .AnyAsync(l => l.CustomerId == target && l.Provider == identity.Provider, cancellationToken);
        if (alreadyHasProvider)
        {
            // One account of each provider per buyer: another Google account is not connected next to the first.
            return new ExternalSignInResult(ExternalSignInOutcome.AlreadyLinked);
        }

        return await TryAddLinkAsync(target, identity, cancellationToken)
            ? new ExternalSignInResult(ExternalSignInOutcome.Linked)
            : new ExternalSignInResult(ExternalSignInOutcome.AlreadyLinked);
    }

    /// <summary>Adds the link. False when the provider account was connected to another buyer at the same moment (the unique index).</summary>
    private async Task<bool> TryAddLinkAsync(Guid customerId, ExternalIdentity identity, CancellationToken cancellationToken)
    {
        context.CustomerExternalLogins.Add(new CustomerExternalLogin
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Provider = identity.Provider,
            ProviderKey = identity.ProviderKey,
            CreatedAt = timeProvider.GetUtcNow(),
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.ChangeTracker.Clear();
            return false;
        }
    }

    private async Task<ExternalSignInResult> SignedInAsync(CustomerUser user, CancellationToken cancellationToken)
    {
        signInManager.AuthenticationScheme = CustomerAuthentication.Scheme;
        await signInManager.SignInAsync(user, isPersistent: true);
        // The address is confirmed (the provider verified it), so the orders placed as a guest with it join the account.
        if (user.EmailConfirmed)
        {
            await repositories.Order.LinkGuestOrdersAsync(user.Id, user.Email!, cancellationToken);
        }

        var linked = await context.CustomerExternalLogins.AsNoTracking()
            .Where(l => l.CustomerId == user.Id).OrderBy(l => l.Provider).Select(l => l.Provider).ToListAsync(cancellationToken);
        var customer = new CustomerDto(user.Email!, user.DisplayName, user.EmailConfirmed, await users.HasPasswordAsync(user), linked);
        return new ExternalSignInResult(ExternalSignInOutcome.SignedIn, new SignedInCustomer(user.Id, customer));
    }

    private static string DisplayNameOf(ExternalIdentity identity, string address)
    {
        var name = string.IsNullOrWhiteSpace(identity.Name) ? address.Split('@')[0] : identity.Name.Trim();
        return name.Length <= MaxNameLength ? name : name[..MaxNameLength];
    }
}
