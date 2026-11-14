using Storefront.Domain.Orders;

namespace Storefront.UnitTests;

public sealed class OrderTransitionsTests
{
    public static TheoryData<OrderStatus, OrderStatus> Allowed => new()
    {
        { OrderStatus.New, OrderStatus.Paid },
        { OrderStatus.New, OrderStatus.Cancelled },
        { OrderStatus.Paid, OrderStatus.InProduction },
        { OrderStatus.InProduction, OrderStatus.ReadyToShip },
        { OrderStatus.ReadyToShip, OrderStatus.Shipped },
        { OrderStatus.Shipped, OrderStatus.Completed },
        { OrderStatus.Cancelled, OrderStatus.Paid },
    };

    [Theory]
    [MemberData(nameof(Allowed))]
    public void TheMovesOfTheOrderLifecycle_AreAllowed(OrderStatus from, OrderStatus to) =>
        Assert.True(OrderTransitions.CanMove(from, to));

    [Fact]
    public void EveryOtherMove_IsForbidden()
    {
        var statuses = Enum.GetValues<OrderStatus>();
        var allowed = Allowed.Select(row => ((OrderStatus)row[0], (OrderStatus)row[1])).ToHashSet();

        var forbiddenThatPass = (
            from source in statuses
            from target in statuses
            where !allowed.Contains((source, target)) && OrderTransitions.CanMove(source, target)
            select $"{source} -> {target}").ToList();

        Assert.Empty(forbiddenThatPass);
    }

    [Fact]
    public void AFinishedOrder_HasNowhereToGo() => Assert.Empty(OrderTransitions.From(OrderStatus.Completed));

    [Fact]
    public void ANewOrder_CannotSkipThePayment() => Assert.False(OrderTransitions.CanMove(OrderStatus.New, OrderStatus.Shipped));
}
