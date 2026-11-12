namespace Storefront.Api.Requests;

/// <summary>Puts units of a variant in the cart. Units of a variant that is already there are added to its line.</summary>
public sealed record AddCartItemRequest(Guid VariantId, int Quantity);
