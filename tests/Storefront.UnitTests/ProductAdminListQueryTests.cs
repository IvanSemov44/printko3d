using Storefront.Application.Catalog.Queries;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

public sealed class ProductAdminListQueryTests
{
    private static ProductAdminListQuery Create(
        string? status = null,
        Guid? categoryId = null,
        string? search = null,
        int page = 1,
        int pageSize = 20) =>
        ProductAdminListQuery.Create(status, categoryId, search, page, pageSize);

    [Fact]
    public void Defaults_AreNoFilters()
    {
        var query = Create();

        Assert.Null(query.Status);
        Assert.Null(query.CategoryId);
        Assert.Null(query.Search);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
    }

    [Theory]
    [InlineData("Draft", ProductStatus.Draft)]
    [InlineData("published", ProductStatus.Published)]
    [InlineData("ARCHIVED", ProductStatus.Archived)]
    public void Status_IsParsedByName(string value, ProductStatus expected) =>
        Assert.Equal(expected, Create(status: value).Status);

    [Theory]
    [InlineData("1")]
    [InlineData("Deleted")]
    public void Status_ThatIsNotAName_IsRejectedOnItsField(string value)
    {
        var error = Assert.Throws<ValidationFailedException>(() => Create(status: value));

        Assert.Contains("status", error.Errors.Keys);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(10_000, true)]
    [InlineData(0, false)]
    [InlineData(10_001, false)]
    public void Page_MustBeBetweenOneAndTheMaximum(int page, bool accepted)
    {
        if (accepted)
        {
            Assert.Equal(page, Create(page: page).Page);
        }
        else
        {
            Assert.Contains("page", Assert.Throws<ValidationFailedException>(() => Create(page: page)).Errors.Keys);
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    public void PageSize_MustBeBetweenOneAndOneHundred(int pageSize, bool accepted)
    {
        if (accepted)
        {
            Assert.Equal(pageSize, Create(pageSize: pageSize).PageSize);
        }
        else
        {
            Assert.Contains("pageSize", Assert.Throws<ValidationFailedException>(() => Create(pageSize: pageSize)).Errors.Keys);
        }
    }

    [Fact]
    public void Search_IsTrimmed_AndBlankMeansNoSearch()
    {
        Assert.Equal("drag", Create(search: "  drag ").Search);
        Assert.Null(Create(search: "   ").Search);
    }

    [Fact]
    public void Search_OfOneHundredCharactersIsAccepted_AndOneHundredAndOneIsRejected()
    {
        Assert.Equal(100, Create(search: new string('a', 100)).Search!.Length);

        var error = Assert.Throws<ValidationFailedException>(() => Create(search: new string('a', 101)));
        Assert.Contains("search", error.Errors.Keys);
    }

    [Fact]
    public void EveryBadParameter_IsListed()
    {
        var error = Assert.Throws<ValidationFailedException>(() => Create(status: "x", page: 0, pageSize: 0));

        Assert.Equal(["page", "pageSize", "status"], error.Errors.Keys.Order());
    }
}
