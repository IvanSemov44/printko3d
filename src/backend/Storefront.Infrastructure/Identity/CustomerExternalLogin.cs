namespace Storefront.Infrastructure.Identity;

/// <summary>The link between a customer and an account at a sign-in provider such as Google (decision 0039). Unique by provider and provider key.</summary>
public sealed class CustomerExternalLogin
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>The scheme name of the provider, for example <c>Google</c>.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The id of the person at the provider. It is not an email and not a secret.</summary>
    public string ProviderKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
