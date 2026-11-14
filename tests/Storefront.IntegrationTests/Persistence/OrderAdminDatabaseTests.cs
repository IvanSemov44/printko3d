using Microsoft.EntityFrameworkCore;
using Storefront.Application.Exceptions;
using Storefront.Application.Orders;
using Storefront.Domain.Orders;
using Storefront.IntegrationTests.Payments;
using static Storefront.IntegrationTests.Payments.PaymentWorld;

namespace Storefront.IntegrationTests.Persistence;

/// <summary>The administrator's list and detail of orders (task 28a) against a real PostgreSQL.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderAdminDatabaseTests(PostgresFixture postgres)
{
    private static Task<Storefront.Application.Catalog.Queries.PagedResult<OrderAdminListItemDto>> ListAsync(
        Setup setup,
        string[]? statuses = null,
        string? search = null,
        int page = 1,
        int pageSize = 20) =>
        InScopeAsync(setup, s => s.OrderAdmin.GetListAsync(
            OrderAdminListQuery.Create(statuses, search, page, pageSize), default));

    private static async Task SetAsync(Setup setup, int number, OrderStatus? status = null, string? email = null, string? name = null)
    {
        await using var context = setup.Database.CreateContext();
        await context.Orders.Where(o => o.Number == number).ExecuteUpdateAsync(s => s
            .SetProperty(o => o.Status, o => status ?? o.Status)
            .SetProperty(o => o.Email, o => email ?? o.Email)
            .SetProperty(o => o.CustomerName, o => name ?? o.CustomerName));
    }

    [Fact]
    public async Task TheList_HasTheNewestFirst_WithTheCountOfLines()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres, firstStock: 20, secondStock: 20);
        var first = await PlaceAsync(setup);
        setup.Time.Advance(TimeSpan.FromMinutes(1));
        var second = await PlaceAsync(setup, withSecond: true);

        var page = await ListAsync(setup);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([second.Number, first.Number], page.Items.Select(i => i.Number));
        Assert.Equal([2, 1], page.Items.Select(i => i.ItemCount));
        Assert.Equal(4500, page.Items[0].Total);
        Assert.Equal("New", page.Items[0].Status);
    }

    [Fact]
    public async Task TheList_FiltersByOneOrMoreStatuses()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres, firstStock: 20, secondStock: 20);
        var a = await PlaceAsync(setup);
        var b = await PlaceAsync(setup);
        var c = await PlaceAsync(setup);
        await SetAsync(setup, b.Number, OrderStatus.Paid);
        await SetAsync(setup, c.Number, OrderStatus.Shipped);

        Assert.Equal([a.Number], (await ListAsync(setup, ["New"])).Items.Select(i => i.Number));
        Assert.Equal(
            new[] { b.Number, c.Number }.Order(),
            (await ListAsync(setup, ["Paid", "Shipped"])).Items.Select(i => i.Number).Order());
    }

    [Fact]
    public async Task TheList_IsPaged_AndSaysHowManyThereAre()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres, firstStock: 20, secondStock: 20);
        for (var i = 0; i < 3; i++)
        {
            await PlaceAsync(setup);
            setup.Time.Advance(TimeSpan.FromMinutes(1));
        }

        var second = await ListAsync(setup, page: 2, pageSize: 2);

        Assert.Equal(3, second.TotalCount);
        Assert.Equal(2, second.TotalPages);
        Assert.Single(second.Items);
    }

    [Fact]
    public async Task TheSearch_FindsByNumber_ByPartOfTheEmail_AndByName_AnyLetterCase()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres, firstStock: 20, secondStock: 20);
        var a = await PlaceAsync(setup);
        var b = await PlaceAsync(setup);
        await SetAsync(setup, a.Number, email: "anna.ivanova@example.test", name: "Anna Ivanova");
        await SetAsync(setup, b.Number, email: "boris@example.test", name: "Boris Petrov");

        Assert.Equal([a.Number], (await ListAsync(setup, search: a.Number.ToString())).Items.Select(i => i.Number));
        Assert.Equal([a.Number], (await ListAsync(setup, search: "IVANOVA@")).Items.Select(i => i.Number));
        Assert.Equal([b.Number], (await ListAsync(setup, search: "petrov")).Items.Select(i => i.Number));
        Assert.Empty((await ListAsync(setup, search: "nobody")).Items);
    }

    [Fact]
    public async Task ThePercentSign_InTheSearch_IsAPlainCharacter()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres, firstStock: 20, secondStock: 20);
        await PlaceAsync(setup);

        Assert.Empty((await ListAsync(setup, search: "%")).Items);
    }

    [Fact]
    public async Task TheDetail_HasTheLines_AndTheForwardMovesOfTheStatus()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres, firstStock: 20, secondStock: 20);
        var order = await PlaceAsync(setup, withSecond: true);
        await SetAsync(setup, order.Number, OrderStatus.Paid);

        var detail = await InScopeAsync(setup, s => s.OrderAdmin.GetAsync(order.Number, default));

        Assert.Equal(order.Number, detail.Order.Number);
        Assert.Equal("Paid", detail.Order.Status);
        Assert.Equal(2, detail.Order.Items.Count);
        Assert.Equal(["InProduction"], detail.NextStatuses);
        Assert.True(detail.CanCancel);
    }

    [Fact]
    public async Task TheDetailOfAnUnknownOrder_IsNotFound()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres, firstStock: 20, secondStock: 20);

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => InScopeAsync(setup, s => s.OrderAdmin.GetAsync(12345678, default)));

        Assert.Equal("order.not_found", error.Code);
    }
}
