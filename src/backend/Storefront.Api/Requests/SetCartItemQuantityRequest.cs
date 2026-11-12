namespace Storefront.Api.Requests;

/// <summary>Replaces the number of units of one line of the cart.</summary>
public sealed record SetCartItemQuantityRequest(int Quantity);
