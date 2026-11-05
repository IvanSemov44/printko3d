using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

public sealed class CatalogInvariantTests
{
    private static Product NewProduct(FulfilmentType type = FulfilmentType.InStock, int? leadTimeDays = null) =>
        new(Guid.CreateVersion7(), "Dragon", "dragon", "Short", "Long", type, leadTimeDays);

    [Fact]
    public void MadeToOrderProduct_WithoutLeadTime_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => NewProduct(FulfilmentType.MadeToOrder));
    }

    [Fact]
    public void MadeToOrderProduct_WithLeadTime_IsAccepted()
    {
        var product = NewProduct(FulfilmentType.MadeToOrder, leadTimeDays: 7);

        Assert.Equal(7, product.LeadTimeDays);
    }

    [Fact]
    public void InStockProduct_WithoutLeadTime_IsAccepted()
    {
        Assert.Null(NewProduct().LeadTimeDays);
    }

    [Fact]
    public void ShortDescription_LongerThan200Characters_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new Product(
            Guid.CreateVersion7(), "Dragon", "dragon", new string('a', 201), "Long", FulfilmentType.InStock));
    }

    [Fact]
    public void Variant_WithNegativePrice_IsRejected()
    {
        var product = NewProduct();

        Assert.Throws<ArgumentOutOfRangeException>(() => product.AddVariant("SKU-1", "Blue", -1, 10));
    }

    [Fact]
    public void Variant_WithNegativeStock_IsRejected()
    {
        var product = NewProduct();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => product.AddVariant("SKU-1", "Blue", 100, 10, stockQuantity: -1));
    }

    [Fact]
    public void Variant_WithZeroPriceAndStock_IsAccepted()
    {
        var product = NewProduct();

        var variant = product.AddVariant("SKU-1", "Blue", 0, 10, stockQuantity: 0);

        Assert.Equal(0, variant.Price);
        Assert.Equal(0, variant.StockQuantity);
    }

    [Fact]
    public void Category_UnderASubcategory_IsRejected()
    {
        var root = new Category("Root", "root");
        var child = new Category("Child", "child", root);

        Assert.Throws<InvalidOperationException>(() => new Category("Grandchild", "grandchild", child));
    }

    [Fact]
    public void Category_UnderARootCategory_IsAccepted()
    {
        var root = new Category("Root", "root");

        var child = new Category("Child", "child", root);

        Assert.Equal(root.Id, child.ParentId);
    }

    [Fact]
    public void Product_WithoutVariants_CannotBePublished()
    {
        var product = NewProduct();

        Assert.Throws<InvalidOperationException>(product.Publish);
        Assert.Equal(ProductStatus.Draft, product.Status);
    }

    [Fact]
    public void Product_WithAVariant_CanBePublished()
    {
        var product = NewProduct();
        product.AddVariant("SKU-1", "Blue", 100, 10);

        product.Publish();

        Assert.Equal(ProductStatus.Published, product.Status);
    }
}
