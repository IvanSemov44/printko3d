namespace Storefront.Application.Customers;

/// <summary>
/// What a social provider (Google, Facebook) said about the person who signed in there (decision 0039). The email counts only when
/// <paramref name="EmailVerified"/> is true: an address that the provider did not verify creates and joins nothing.
/// </summary>
public sealed record ExternalIdentity(string Provider, string ProviderKey, string? Email, bool EmailVerified, string? Name);

/// <summary>The providers that are turned on, as the sign-in page and the profile read them.</summary>
public sealed record ExternalProvidersDto(IReadOnlyList<string> Providers);

/// <summary>What came of a sign-in through a provider.</summary>
public enum ExternalSignInOutcome
{
    /// <summary>The buyer is signed in: an account that already had this provider, or a new account made from it.</summary>
    SignedIn,

    /// <summary>A signed-in buyer connected the provider to their account.</summary>
    Linked,

    /// <summary>The address has a confirmed account of its own: the buyer signs in with the password and connects the provider from the profile.</summary>
    AccountExists,

    /// <summary>The provider gave no address, or did not verify it.</summary>
    EmailNotVerified,

    /// <summary>The provider account is already connected to another account of the shop.</summary>
    AlreadyLinked,
}

/// <summary>The result of <see cref="ICustomerExternalLoginService.SignInAsync"/>. <paramref name="Customer"/> is set when the buyer was signed in.</summary>
public sealed record ExternalSignInResult(ExternalSignInOutcome Outcome, SignedInCustomer? Customer = null);

/// <summary>Sign-in and connecting through a social provider (decision 0039). Implemented where ASP.NET Identity is.</summary>
public interface ICustomerExternalLoginService
{
    /// <summary>
    /// Signs the buyer in with what the provider said. A provider that is already connected signs its account in. Otherwise, with
    /// a verified address: a new address makes a new account (confirmed, with no password); an address of an account that was never
    /// confirmed is confirmed and its password is taken away (someone may have registered it without owning it); an address of a
    /// confirmed account is refused (<see cref="ExternalSignInOutcome.AccountExists"/>). With <paramref name="linkTo"/> the provider
    /// is connected to that account instead and nobody is signed in.
    /// </summary>
    Task<ExternalSignInResult> SignInAsync(ExternalIdentity identity, Guid? linkTo, CancellationToken cancellationToken);
}
