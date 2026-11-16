using Microsoft.AspNetCore.Identity;

namespace Storefront.Infrastructure.Identity;

/// <summary>A buyer's account (decision 0039). Not an Entity: Identity owns its keys and columns. It has no roles; the customer's own sign-in scheme recognises it.</summary>
public sealed class CustomerUser : IdentityUser<Guid>
{
    /// <summary>The name shown to the buyer. Optional: an account made by a social sign-in takes it from the provider.</summary>
    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
