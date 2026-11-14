namespace Storefront.Domain.Orders;

public enum OrderStatus
{
    New,

    /// <summary>The provider confirmed the payment.</summary>
    Paid,

    /// <summary>The deadline for the payment passed, or the order was refused. The stock is given back.</summary>
    Cancelled,
}
