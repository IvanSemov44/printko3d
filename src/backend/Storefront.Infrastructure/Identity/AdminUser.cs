using Microsoft.AspNetCore.Identity;

namespace Storefront.Infrastructure.Identity;

/// <summary>An account that can sign in to the admin panel. Not an Entity: Identity owns its keys and columns.</summary>
public sealed class AdminUser : IdentityUser<Guid>;
