namespace Storefront.Application.Carts.Queries;

/// <summary>What a shopper can do with a line of the cart right now. Computed on every read, never stored.</summary>
public enum CartLineState
{
    Available,
    InsufficientStock,
    Inactive,
    Archived,
}
