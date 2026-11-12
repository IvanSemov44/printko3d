namespace Storefront.Domain.Carts;

/// <summary>
/// An anonymous shopping cart. The buyer is recognised by a random token in a cookie;
/// only the SHA-256 hash of the token is stored, so a leaked database does not give access to carts.
/// </summary>
public sealed class Cart : Entity
{
    public const int MaxItems = 50;

    public const int MaxQuantityPerItem = 20;

    public byte[] TokenHash { get; set; } = [];

    public List<CartItem> Items { get; set; } = [];
}
