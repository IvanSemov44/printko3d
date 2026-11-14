using System.Text.Json.Serialization;
using Storefront.Application.Payments;

namespace Storefront.Api.Requests;

/// <summary>What the test payment page sends: how the test payment ends. The sum is never sent; it is the one of the payment.</summary>
/// <param name="Outcome">Paid or Failed.</param>
public sealed record CompleteTestPaymentRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] PaymentOutcome Outcome);
