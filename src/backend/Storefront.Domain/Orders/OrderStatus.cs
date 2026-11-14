namespace Storefront.Domain.Orders;

public enum OrderStatus
{
    New,

    /// <summary>The provider confirmed the payment.</summary>
    Paid,

    /// <summary>The order is being made.</summary>
    InProduction,

    /// <summary>Everything is made and packed; the courier has not taken it yet.</summary>
    ReadyToShip,

    /// <summary>The courier took the parcel.</summary>
    Shipped,

    /// <summary>The buyer has the order. Set by hand by the administrator.</summary>
    Completed,

    /// <summary>The deadline for the payment passed, or the order was refused. The stock is given back.</summary>
    Cancelled,
}
