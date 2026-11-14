namespace Storefront.Application.Orders;

/// <summary>Codes of order errors that a caller outside the Application project needs to recognise.</summary>
public static class OrderErrorCodes
{
    /// <summary>Someone took the last pieces between the cart check and the save (409).</summary>
    public const string StockChanged = "order.stock_changed";
}
