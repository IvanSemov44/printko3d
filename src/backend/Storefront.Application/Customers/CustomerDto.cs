namespace Storefront.Application.Customers;

/// <summary>The signed-in buyer, as the shop sees them (decision 0039).</summary>
/// <param name="Email">The address the account was made with.</param>
/// <param name="DisplayName">The name shown to the buyer.</param>
/// <param name="EmailConfirmed">True after the buyer opened the link in the confirmation email, or signed in through a provider that confirmed the address.</param>
/// <param name="HasPassword">False for an account made only by a social sign-in: it can set a password through "forgot password".</param>
public sealed record CustomerDto(string Email, string DisplayName, bool EmailConfirmed, bool HasPassword);

/// <summary>The buyer who has just signed in, with the id of the account for what the server does next (joining the carts). The id is not sent to the browser.</summary>
public sealed record SignedInCustomer(Guid Id, CustomerDto Customer);
