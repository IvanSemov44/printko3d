using Storefront.Application.Exceptions;
using Storefront.Application.Orders;
using Storefront.Domain.Orders;

namespace Storefront.UnitTests;

public sealed class OrderAdminListQueryTests
{
    private static OrderAdminListQuery Create(
        string[]? statuses = null,
        string? search = null,
        int page = 1,
        int pageSize = 20) =>
        OrderAdminListQuery.Create(statuses, search, page, pageSize);

    [Fact]
    public void Defaults_AreNoFilters()
    {
        var query = Create();

        Assert.Empty(query.Statuses);
        Assert.Null(query.Search);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
    }

    [Fact]
    public void Statuses_AreParsedByName_AnyLetterCase_WithoutDuplicates()
    {
        var query = Create(statuses: ["paid", "Paid", "INPRODUCTION"]);

        Assert.Equal([OrderStatus.Paid, OrderStatus.InProduction], query.Statuses);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("Deleted")]
    public void AStatusThatIsNotAName_IsRejectedOnItsField(string value)
    {
        var error = Assert.Throws<ValidationFailedException>(() => Create(statuses: [value]));

        Assert.Contains("status", error.Errors.Keys);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(10_000, 100, true)]
    [InlineData(0, 20, false)]
    [InlineData(10_001, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    public void PageAndPageSize_HaveLimits(int page, int pageSize, bool valid)
    {
        var error = Record.Exception(() => Create(page: page, pageSize: pageSize));

        Assert.Equal(valid, error is null);
        Assert.True(valid || error is ValidationFailedException);
    }

    [Fact]
    public void TheSearch_IsTrimmed_AndCapped()
    {
        Assert.Equal("anna", Create(search: "  anna ").Search);
        Assert.Null(Create(search: "   ").Search);
        var error = Assert.Throws<ValidationFailedException>(() => Create(search: new string('a', 101)));
        Assert.Contains("search", error.Errors.Keys);
    }
}
