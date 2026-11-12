namespace Storefront.Domain.Carts;

/// <summary>A line of a cart. Only the variant and the quantity are stored; price and name are read from the variant.</summary>
public sealed class CartItem : Entity
{
    public Guid CartId { get; set; }

    public Guid VariantId { get; set; }

    public int Quantity { get; set; }
}
