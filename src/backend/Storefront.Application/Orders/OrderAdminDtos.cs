namespace Storefront.Application.Orders;

/// <summary>A row of the administrator's list of orders. Money is whole numbers in the smallest unit of <paramref name="Currency"/>.</summary>
public sealed record OrderAdminListItemDto(
    int Number,
    string Status,
    string Currency,
    long Total,
    string CustomerName,
    string Email,
    int ItemCount,
    DateTimeOffset CreatedAt);

/// <summary>How many orders have a status. The list of the panel shows it next to each status filter.</summary>
public sealed record OrderStatusCountDto(string Status, int Count);

/// <summary>
/// One order for the administrator. <paramref name="NextStatuses"/> are the production steps the administrator can take
/// now (decision 0037), so the panel does not repeat the rules.
/// </summary>
public sealed record OrderAdminDto(OrderDto Order, IReadOnlyList<string> NextStatuses, bool CanCancel);

/// <summary>
/// The order after the administrator refused it. <paramref name="RefundByHand"/> is true when the buyer had paid: the
/// provider has no refund yet (decision 0037), so the money goes back outside the shop.
/// </summary>
public sealed record OrderCancelledDto(OrderAdminDto Order, bool RefundByHand);
