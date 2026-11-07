using Storefront.Application.Catalog.Queries;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

public class ProductAvailabilityTests
{
    [Theory]
    [InlineData(FulfilmentType.InStock, true, true)]
    [InlineData(FulfilmentType.InStock, false, false)]
    [InlineData(FulfilmentType.MadeToOrder, false, true)]
    [InlineData(FulfilmentType.MadeToOrder, true, true)]
    public void Product(FulfilmentType type, bool anyInStock, bool expected) =>
        Assert.Equal(expected, ProductAvailability.IsAvailable(type, anyInStock));

    [Theory]
    [InlineData(FulfilmentType.InStock, 5, true)]
    [InlineData(FulfilmentType.InStock, 1, true)]
    [InlineData(FulfilmentType.InStock, 0, false)]
    [InlineData(FulfilmentType.InStock, null, false)]
    [InlineData(FulfilmentType.MadeToOrder, null, true)]
    [InlineData(FulfilmentType.MadeToOrder, 0, true)]
    public void Variant(FulfilmentType type, int? stock, bool expected) =>
        Assert.Equal(expected, ProductAvailability.IsVariantAvailable(type, stock));

    [Theory]
    [InlineData(0, 12, 0)]
    [InlineData(1, 12, 1)]
    [InlineData(12, 12, 1)]
    [InlineData(13, 12, 2)]
    public void TotalPages_RoundsUp(int total, int pageSize, int expected) =>
        Assert.Equal(expected, new PagedResult<int>([], 1, pageSize, total).TotalPages);
}
