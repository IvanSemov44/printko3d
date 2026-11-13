namespace Storefront.Application.Orders;

/// <summary>
/// What the buyer fills in. The country is always Bulgaria for now. <paramref name="ExpectedTotal"/> is the total the
/// buyer saw in the preview; the order is refused if the cart is worth something else now.
/// </summary>
public sealed record PlaceOrderInput(
    string Name,
    string Email,
    string Phone,
    string Street,
    string City,
    string PostalCode,
    bool TermsAccepted,
    long ExpectedTotal);
