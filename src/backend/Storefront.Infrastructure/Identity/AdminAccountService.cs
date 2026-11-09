using Microsoft.AspNetCore.Identity;
using Storefront.Application.Auth;

namespace Storefront.Infrastructure.Identity;

internal sealed class AdminAccountService(UserManager<AdminUser> users, RoleManager<IdentityRole<Guid>> roles)
    : IAdminAccountService
{
    public async Task<EnsureAdminResult> EnsureAdminAsync(
        string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await roles.RoleExistsAsync(AdminRole.Name))
        {
            Check(await roles.CreateAsync(new IdentityRole<Guid>(AdminRole.Name)));
        }

        var created = false;
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AdminUser { UserName = email, Email = email };
            Check(await users.CreateAsync(user, password));
            created = true;
        }

        if (!await users.IsInRoleAsync(user, AdminRole.Name))
        {
            Check(await users.AddToRoleAsync(user, AdminRole.Name));
        }

        return created ? EnsureAdminResult.Created : EnsureAdminResult.AlreadyExists;
    }

    // Identity error descriptions can repeat the email, so only the codes go further.
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new AdminAccountException([.. result.Errors.Select(e => e.Code)]);
        }
    }
}
