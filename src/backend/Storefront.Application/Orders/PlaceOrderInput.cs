namespace Storefront.Application.Orders;

/// <summary>
/// What the buyer fills in. The country is always Bulgaria for now. <paramref name="ExpectedTotal"/> is the total the
/// buyer saw in the preview, with the delivery; the order is refused if the cart or the delivery is worth something
/// else now. <paramref name="ShippingMethod"/> is the code of the delivery method the buyer chose.
/// </summary>
public sealed record PlaceOrderInput(
    string Name,
    string Email,
    string Phone,
    string Street,
    string City,
    string PostalCode,
    bool TermsAccepted,
    string ShippingMethod,
    long ExpectedTotal);
