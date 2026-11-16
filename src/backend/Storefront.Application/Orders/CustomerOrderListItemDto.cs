namespace Storefront.Application.Orders;

/// <summary>A row of the history of orders of a buyer. Money is whole numbers in the smallest unit of <paramref name="Currency"/>.</summary>
public sealed record CustomerOrderListItemDto(
    int Number,
    string Status,
    string Currency,
    long Total,
    int ItemCount,
    DateTimeOffset CreatedAt);
