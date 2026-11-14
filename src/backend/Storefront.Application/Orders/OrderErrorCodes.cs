namespace Storefront.Application.Orders;

/// <summary>Codes of order errors that a caller outside the Application project needs to recognise.</summary>
public static class OrderErrorCodes
{
    /// <summary>Someone took the last pieces between the cart check and the save (409).</summary>
    public const string StockChanged = "order.stock_changed";

    /// <summary>The order was not found, or the number and the email do not belong together (404).</summary>
    public const string NotFound = "order.not_found";

    /// <summary>The move between statuses is not allowed, or the order changed at the same moment (409).</summary>
    public const string StatusTransitionInvalid = "order.status_transition_invalid";
}
