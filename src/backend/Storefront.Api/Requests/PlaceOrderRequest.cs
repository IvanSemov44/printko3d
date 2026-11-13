using Storefront.Application.Orders;

namespace Storefront.Api.Requests;

/// <summary>
/// What the buyer fills in at the checkout. <paramref name="ExpectedTotal"/> is the total of the cart the buyer saw,
/// in the smallest currency unit. The country is always Bulgaria for now, so it is not sent.
/// </summary>
public sealed record PlaceOrderRequest(
    string Name,
    string Email,
    string Phone,
    string Street,
    string City,
    string PostalCode,
    bool TermsAccepted,
    long ExpectedTotal)
{
    public PlaceOrderInput ToInput() =>
        new(Name, Email, Phone, Street, City, PostalCode, TermsAccepted, ExpectedTotal);
}
