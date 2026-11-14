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

/// <summary>
/// One order for the administrator. <paramref name="NextStatuses"/> are the production steps the administrator can take
/// now (decision 0037), so the panel does not repeat the rules.
/// </summary>
public sealed record OrderAdminDto(OrderDto Order, IReadOnlyList<string> NextStatuses);
