using Storefront.Application.Catalog.Queries;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

public class ProductListQueryTests
{
    private static ProductListQuery Create(
        string? category = null,
        string? fulfilmentType = null,
        string? sort = null,
        int page = 1,
        int pageSize = 12) =>
        ProductListQuery.Create(category, fulfilmentType, sort, page, pageSize);

    [Fact]
    public void Defaults_AreFirstPageOfTwelveSortedByName()
    {
        var query = Create();

        Assert.Equal(1, query.Page);
        Assert.Equal(12, query.PageSize);
        Assert.Equal(ProductSort.NameAscending, query.Sort);
        Assert.Null(query.CategorySlug);
        Assert.Null(query.FulfilmentType);
    }

    [Theory]
    [InlineData("name", ProductSort.NameAscending)]
    [InlineData("-name", ProductSort.NameDescending)]
    [InlineData("price", ProductSort.PriceAscending)]
    [InlineData("-price", ProductSort.PriceDescending)]
    public void Sort_IsParsed(string value, ProductSort expected) =>
        Assert.Equal(expected, Create(sort: value).Sort);

    [Theory]
    [InlineData("InStock", FulfilmentType.InStock)]
    [InlineData("madetoorder", FulfilmentType.MadeToOrder)]
    public void FulfilmentType_IsParsed(string value, FulfilmentType expected) =>
        Assert.Equal(expected, Create(fulfilmentType: value).FulfilmentType);

    [Fact]
    public void MaxPageSize_IsAccepted() => Assert.Equal(48, Create(pageSize: 48).PageSize);

    [Theory]
    [InlineData("page", 0, 12)]
    [InlineData("page", -1, 12)]
    [InlineData("page", 10_001, 12)]
    [InlineData("page", int.MaxValue, 48)]
    [InlineData("pageSize", 1, 0)]
    [InlineData("pageSize", 1, 49)]
    [InlineData("pageSize", 1, 1000)]
    public void OutOfRangePaging_Fails(string parameter, int page, int pageSize)
    {
        var exception = Assert.Throws<ValidationFailedException>(() => Create(page: page, pageSize: pageSize));

        Assert.Contains(parameter, exception.Errors.Keys);
    }

    [Theory]
    [InlineData("cheapest")]
    [InlineData("Name")]
    [InlineData("--price")]
    public void UnknownSort_Fails(string value)
    {
        var exception = Assert.Throws<ValidationFailedException>(() => Create(sort: value));

        Assert.Contains("sort", exception.Errors.Keys);
    }

    [Theory]
    [InlineData("Pickup")]
    [InlineData("1")]
    [InlineData("7")]
    public void UnknownFulfilmentType_Fails(string value)
    {
        var exception = Assert.Throws<ValidationFailedException>(() => Create(fulfilmentType: value));

        Assert.Contains("fulfilmentType", exception.Errors.Keys);
    }

    [Fact]
    public void EveryBadParameter_IsReported()
    {
        var exception = Assert.Throws<ValidationFailedException>(() => Create("x", "bad", "bad", 0, 0));

        Assert.Equal(["fulfilmentType", "page", "pageSize", "sort"], exception.Errors.Keys.Order());
    }

    [Fact]
    public void BlankCategory_MeansNoFilter() => Assert.Null(Create(category: " ").CategorySlug);
}
