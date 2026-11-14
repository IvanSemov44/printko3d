using Storefront.Application.Orders;

namespace Storefront.Api.Requests;

/// <summary>
/// What the buyer fills in at the checkout. <paramref name="ShippingMethod"/> is the code of the delivery the buyer
/// chose (from `GET /api/v1/shipping/quotes`). <paramref name="ExpectedTotal"/> is the total the buyer saw, with the
/// delivery, in the smallest currency unit. The country is always Bulgaria for now, so it is not sent.
/// </summary>
public sealed record PlaceOrderRequest(
    string Name,
    string Email,
    string Phone,
    string Street,
    string City,
    string PostalCode,
    bool TermsAccepted,
    string ShippingMethod,
    long ExpectedTotal)
{
    public PlaceOrderInput ToInput() =>
        new(Name, Email, Phone, Street, City, PostalCode, TermsAccepted, ShippingMethod, ExpectedTotal);
}
