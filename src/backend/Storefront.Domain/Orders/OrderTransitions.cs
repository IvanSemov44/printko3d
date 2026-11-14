namespace Storefront.Domain.Orders;

/// <summary>
/// The moves between statuses that an order may make (decision 0036). One place, so that the payment, the expiry and
/// the administrator all follow the same rules. Anything not listed is forbidden.
/// </summary>
public static class OrderTransitions
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.New] = [OrderStatus.Paid, OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.InProduction],
        [OrderStatus.InProduction] = [OrderStatus.ReadyToShip],
        [OrderStatus.ReadyToShip] = [OrderStatus.Shipped],
        [OrderStatus.Shipped] = [OrderStatus.Completed],
        // A payment that arrives after the order was cancelled brings it back (task 26).
        [OrderStatus.Cancelled] = [OrderStatus.Paid],
    };

    public static bool CanMove(OrderStatus from, OrderStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>
    /// The production steps the administrator takes by hand (decision 0037): one step forward from <c>Paid</c> to
    /// <c>Completed</c>. Payment moves <c>New</c> and <c>Cancelled</c> orders to <c>Paid</c>, and the refusal is a separate
    /// action, so neither is a step here.
    /// </summary>
    public static IReadOnlyList<OrderStatus> AdminMoves(OrderStatus from) =>
        From(from).Where(to => to is not (OrderStatus.Paid or OrderStatus.Cancelled)).ToList();

    /// <summary>The statuses an order can move to from <paramref name="from"/>. Empty for a finished order.</summary>
    public static IReadOnlyList<OrderStatus> From(OrderStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];
}
