using Storefront.Application.Carts;
using Storefront.Domain.Carts;

namespace Storefront.UnitTests;

public sealed class CartCleanupTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeRepositoryManager _repositories = new();

    private CartCleanupService Service(int batchSize = 500) => new(_repositories, new FixedTimeProvider(Now), batchSize);

    private void AddCart(TimeSpan idle, bool hasItems)
    {
        var cart = new Cart { TokenHash = Guid.NewGuid().ToByteArray(), UpdatedAt = Now - idle };
        if (hasItems)
        {
            cart.Items.Add(new CartItem { CartId = cart.Id, VariantId = Guid.NewGuid(), Quantity = 1 });
        }

        _repositories.Carts.Add(cart);
    }

    [Fact]
    public async Task ACartWithItems_IsDeletedAfterThirtyDaysWithoutAChange()
    {
        AddCart(TimeSpan.FromDays(30) + TimeSpan.FromSeconds(1), hasItems: true);

        var deleted = await Service().DeleteStaleAsync(default);

        Assert.Equal(1, deleted);
        Assert.Empty(_repositories.Carts);
    }

    [Fact]
    public async Task ACartWithItems_IsKeptJustBeforeThirtyDays()
    {
        AddCart(TimeSpan.FromDays(30) - TimeSpan.FromSeconds(1), hasItems: true);

        var deleted = await Service().DeleteStaleAsync(default);

        Assert.Equal(0, deleted);
        Assert.Single(_repositories.Carts);
    }

    [Fact]
    public async Task AnEmptyCart_IsDeletedAfterSevenDays()
    {
        AddCart(TimeSpan.FromDays(7) + TimeSpan.FromSeconds(1), hasItems: false);

        var deleted = await Service().DeleteStaleAsync(default);

        Assert.Equal(1, deleted);
        Assert.Empty(_repositories.Carts);
    }

    [Fact]
    public async Task AnEmptyCart_IsKeptJustBeforeSevenDays()
    {
        AddCart(TimeSpan.FromDays(7) - TimeSpan.FromSeconds(1), hasItems: false);

        await Service().DeleteStaleAsync(default);

        Assert.Single(_repositories.Carts);
    }

    [Fact]
    public async Task ACartWithItems_IsKeptAtEightDays_WhileAnEmptyOneOfTheSameAgeGoes()
    {
        AddCart(TimeSpan.FromDays(8), hasItems: true);
        AddCart(TimeSpan.FromDays(8), hasItems: false);

        var deleted = await Service().DeleteStaleAsync(default);

        Assert.Equal(1, deleted);
        Assert.NotEmpty(Assert.Single(_repositories.Carts).Items);
    }

    [Theory]
    [InlineData(5, 2)]
    [InlineData(4, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 5)]
    public async Task StaleCarts_AreDeletedInBatches_UntilNoneIsLeft(int staleCarts, int batchSize)
    {
        for (var i = 0; i < staleCarts; i++)
        {
            AddCart(TimeSpan.FromDays(40), hasItems: true);
        }

        AddCart(TimeSpan.FromDays(1), hasItems: true);

        var deleted = await Service(batchSize).DeleteStaleAsync(default);

        Assert.Equal(staleCarts, deleted);
        Assert.Single(_repositories.Carts);
    }

    [Fact]
    public async Task ABatchNeverHoldsMoreThanTheBatchSize()
    {
        for (var i = 0; i < 5; i++)
        {
            AddCart(TimeSpan.FromDays(40), hasItems: true);
        }

        await Service(batchSize: 2).DeleteStaleAsync(default);

        // 2 + 2 + 1 for the carts with items, then one empty-cart pass that finds nothing.
        Assert.Equal(4, _repositories.CartDeleteCalls);
    }

    [Fact]
    public async Task WithNothingToDelete_NothingIsDeleted()
    {
        AddCart(TimeSpan.FromHours(1), hasItems: true);

        Assert.Equal(0, await Service().DeleteStaleAsync(default));
    }

    [Fact]
    public async Task ACancelledRun_StopsWithoutDeleting()
    {
        AddCart(TimeSpan.FromDays(40), hasItems: true);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service().DeleteStaleAsync(source.Token));

        Assert.Single(_repositories.Carts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ABatchSizeBelowOne_IsRefused(int batchSize) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Service(batchSize));
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
