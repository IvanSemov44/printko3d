using Storefront.Application.Catalog;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

/// <summary>The rules of creating and editing products and variants in the admin panel (task 12).</summary>
public sealed class ProductManagementTests
{
    private readonly FakeRepositoryManager _repositories = new();
    private readonly ServiceManager _services;
    private Guid _categoryId;

    public ProductManagementTests() => _services = new ServiceManager(_repositories, new FakeImageStorage());

    private async Task<Guid> CategoryAsync()
    {
        if (_categoryId == Guid.Empty)
        {
            _categoryId = await _services.Category.CreateAsync(new NewCategory("Figures", "figures"), default);
        }

        return _categoryId;
    }

    private async Task<NewProduct> NewProductAsync(
        string name = "Dragon",
        string slug = "dragon",
        string shortDescription = "Short",
        FulfilmentType type = FulfilmentType.InStock,
        int? leadTimeDays = null) =>
        new(await CategoryAsync(), name, slug, shortDescription, "Long", type, leadTimeDays);

    private async Task<Guid> CreateProductAsync(
        string slug = "dragon",
        FulfilmentType type = FulfilmentType.InStock,
        int? leadTimeDays = null) =>
        await _services.Product.CreateAsync(await NewProductAsync(slug: slug, type: type, leadTimeDays: leadTimeDays), default);

    private static NewVariant Variant(
        string sku = "SKU-1",
        long price = 100,
        long? compareAtPrice = null,
        int? stock = 5,
        int weight = 10,
        IReadOnlyDictionary<string, string>? attributes = null,
        bool isActive = true,
        int sortOrder = 0) =>
        new(sku, "Blue", price, weight, compareAtPrice, stock, attributes, isActive, sortOrder);

    private Product Stored(Guid id) => _repositories.Products.Single(p => p.Id == id);

    private static UpdateProduct Edit(
        Product product,
        string? name = null,
        string? slug = null,
        Guid? categoryId = null,
        FulfilmentType? type = null,
        int? leadTimeDays = null,
        uint? version = null,
        IReadOnlyDictionary<Guid, int>? variantStock = null) =>
        new(
            categoryId ?? product.CategoryId,
            name ?? product.Name,
            slug ?? product.Slug,
            product.ShortDescription,
            product.Description,
            type ?? product.FulfilmentType,
            leadTimeDays ?? product.LeadTimeDays,
            product.SeoTitle,
            product.SeoDescription,
            version ?? product.Version,
            variantStock);

    // --- Creating a product ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithoutName_IsRejectedOnTheNameField(string name)
    {
        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            async () => await _services.Product.CreateAsync(await NewProductAsync(name: name), default));

        Assert.Contains("name", error.Errors.Keys);
        Assert.Empty(_repositories.Products);
    }

    [Fact]
    public async Task Create_NameOf200CharactersIsAccepted_And201IsRejected()
    {
        await _services.Product.CreateAsync(await NewProductAsync(name: new string('a', 200)), default);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(async () =>
            await _services.Product.CreateAsync(await NewProductAsync(name: new string('a', 201), slug: "other"), default));
        Assert.Contains("name", error.Errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Dragon")]
    [InlineData("two words")]
    [InlineData("-dragon")]
    [InlineData("dragon-")]
    [InlineData("dra--gon")]
    [InlineData("dragon\n")]
    public async Task Create_WithABadSlug_IsRejectedOnTheSlugField(string slug)
    {
        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            async () => await _services.Product.CreateAsync(await NewProductAsync(slug: slug), default));

        Assert.Contains("slug", error.Errors.Keys);
    }

    [Fact]
    public async Task Create_SlugOf200CharactersIsAccepted_And201IsRejected()
    {
        await _services.Product.CreateAsync(await NewProductAsync(slug: new string('a', 200)), default);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(async () =>
            await _services.Product.CreateAsync(await NewProductAsync(slug: new string('a', 201)), default));
        Assert.Contains("slug", error.Errors.Keys);
    }

    [Fact]
    public async Task Create_ShortDescriptionOf200CharactersIsAccepted_And201IsRejectedOnItsField()
    {
        await _services.Product.CreateAsync(await NewProductAsync(shortDescription: new string('a', 200)), default);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(async () =>
            await _services.Product.CreateAsync(
                await NewProductAsync(slug: "other", shortDescription: new string('a', 201)), default));
        Assert.Contains("shortDescription", error.Errors.Keys);
    }

    [Fact]
    public async Task Create_WithATakenSlug_IsAConflictWithTheProductCode()
    {
        await CreateProductAsync("dragon");

        var error = await Assert.ThrowsAsync<ConflictException>(() => CreateProductAsync("dragon"));

        Assert.Equal("product.slug_taken", error.Code);
        Assert.Single(_repositories.Products);
    }

    [Fact]
    public async Task Create_InAnUnknownCategory_IsNotFoundWithTheProductCode()
    {
        var product = new NewProduct(Guid.CreateVersion7(), "Dragon", "dragon", "Short", "Long", FulfilmentType.InStock);

        var error = await Assert.ThrowsAsync<NotFoundException>(() => _services.Product.CreateAsync(product, default));

        Assert.Equal("product.category_not_found", error.Code);
    }

    [Fact]
    public async Task Create_InAnInactiveCategory_IsAccepted()
    {
        var categoryId = await CategoryAsync();
        _repositories.Categories.Single(c => c.Id == categoryId).IsActive = false;

        var id = await CreateProductAsync();

        Assert.Equal(categoryId, Stored(id).CategoryId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_MadeToOrderWithALeadTimeThatIsNotPositive_IsRejectedOnItsField(int leadTimeDays)
    {
        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => CreateProductAsync(type: FulfilmentType.MadeToOrder, leadTimeDays: leadTimeDays));

        Assert.Contains("leadTimeDays", error.Errors.Keys);
    }

    [Fact]
    public async Task Create_MadeToOrderWithALeadTimeOfOneDay_IsAccepted()
    {
        var id = await CreateProductAsync(type: FulfilmentType.MadeToOrder, leadTimeDays: 1);

        Assert.Equal(1, Stored(id).LeadTimeDays);
    }

    [Fact]
    public async Task Create_InStockWithALeadTime_StoresNoLeadTime()
    {
        var id = await CreateProductAsync(type: FulfilmentType.InStock, leadTimeDays: 5);

        Assert.Null(Stored(id).LeadTimeDays);
    }

    [Fact]
    public async Task Create_StartsAsDraftWithoutVariants()
    {
        var id = await CreateProductAsync();

        Assert.Equal(ProductStatus.Draft, Stored(id).Status);
        Assert.Empty(Stored(id).Variants);
    }

    // --- Editing a product ---

    [Fact]
    public async Task Update_ChangesTheFields()
    {
        var id = await CreateProductAsync();
        var product = Stored(id);

        await _services.Product.UpdateAsync(id, Edit(product, name: "Big dragon", slug: "big-dragon"), default);

        Assert.Equal("Big dragon", product.Name);
        Assert.Equal("big-dragon", product.Slug);
    }

    [Fact]
    public async Task Update_OfAnUnknownProduct_IsNotFoundWithTheProductCode()
    {
        var id = await CreateProductAsync();
        var edit = Edit(Stored(id));

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.Product.UpdateAsync(Guid.CreateVersion7(), edit, default));

        Assert.Equal("product.not_found", error.Code);
    }

    [Fact]
    public async Task Update_WithAStaleVersion_IsAVersionConflict_AndChangesNothing()
    {
        var id = await CreateProductAsync();
        var product = Stored(id);
        product.Version = 5;

        var error = await Assert.ThrowsAsync<ConflictException>(
            async () => await _services.Product.UpdateAsync(id, Edit(product, name: "Other", version: 4), default));

        Assert.Equal("product.version_conflict", error.Code);
    }

    [Fact]
    public async Task Update_KeepingItsOwnSlug_IsAccepted()
    {
        var id = await CreateProductAsync();

        await _services.Product.UpdateAsync(id, Edit(Stored(id), name: "Renamed"), default);

        Assert.Equal("Renamed", Stored(id).Name);
    }

    [Fact]
    public async Task Update_ToTheSlugOfAnotherProduct_IsAConflictWithTheProductCode()
    {
        await CreateProductAsync("dragon");
        var id = await CreateProductAsync("phoenix");

        var error = await Assert.ThrowsAsync<ConflictException>(
            async () => await _services.Product.UpdateAsync(id, Edit(Stored(id), slug: "dragon"), default));

        Assert.Equal("product.slug_taken", error.Code);
        Assert.Equal("phoenix", Stored(id).Slug);
    }

    [Fact]
    public async Task Update_ToAnUnknownCategory_IsNotFoundWithTheProductCode()
    {
        var id = await CreateProductAsync();

        var error = await Assert.ThrowsAsync<NotFoundException>(async () =>
            await _services.Product.UpdateAsync(id, Edit(Stored(id), categoryId: Guid.CreateVersion7()), default));

        Assert.Equal("product.category_not_found", error.Code);
    }

    [Fact]
    public async Task Update_InStockToMadeToOrder_NeedsALeadTime()
    {
        var id = await CreateProductAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(async () =>
            await _services.Product.UpdateAsync(
                id, Edit(Stored(id), type: FulfilmentType.MadeToOrder), default));

        Assert.Contains("leadTimeDays", error.Errors.Keys);
        Assert.Equal(FulfilmentType.InStock, Stored(id).FulfilmentType);
    }

    [Fact]
    public async Task Update_InStockToMadeToOrder_ClearsTheStockOfEveryVariant()
    {
        var id = await CreateProductAsync();
        await _services.Product.AddVariantAsync(id, Variant("A", stock: 3), default);
        await _services.Product.AddVariantAsync(id, Variant("B", stock: 0), default);

        await _services.Product.UpdateAsync(
            id, Edit(Stored(id), type: FulfilmentType.MadeToOrder, leadTimeDays: 7), default);

        Assert.Equal(7, Stored(id).LeadTimeDays);
        Assert.All(Stored(id).Variants, v => Assert.Null(v.StockQuantity));
    }

    [Fact]
    public async Task Update_MadeToOrderToInStock_NeedsTheStockOfEveryVariant()
    {
        var id = await CreateProductAsync(type: FulfilmentType.MadeToOrder, leadTimeDays: 7);
        await _services.Product.AddVariantAsync(id, Variant("A", stock: null), default);
        await _services.Product.AddVariantAsync(id, Variant("B", stock: null), default);
        var first = Stored(id).Variants.First();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(async () =>
            await _services.Product.UpdateAsync(
                id,
                Edit(
                    Stored(id),
                    type: FulfilmentType.InStock,
                    variantStock: new Dictionary<Guid, int> { [first.Id] = 2 }),
                default));

        Assert.Contains("variantStock", error.Errors.Keys);
        Assert.Equal(FulfilmentType.MadeToOrder, Stored(id).FulfilmentType);
    }

    [Fact]
    public async Task Update_MadeToOrderToInStock_RejectsNegativeStock()
    {
        var id = await CreateProductAsync(type: FulfilmentType.MadeToOrder, leadTimeDays: 7);
        await _services.Product.AddVariantAsync(id, Variant("A", stock: null), default);
        var variant = Stored(id).Variants.Single();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(async () =>
            await _services.Product.UpdateAsync(
                id,
                Edit(
                    Stored(id),
                    type: FulfilmentType.InStock,
                    variantStock: new Dictionary<Guid, int> { [variant.Id] = -1 }),
                default));

        Assert.Contains("variantStock", error.Errors.Keys);
    }

    [Fact]
    public async Task Update_MadeToOrderToInStock_SetsTheStockAndClearsTheLeadTime()
    {
        var id = await CreateProductAsync(type: FulfilmentType.MadeToOrder, leadTimeDays: 7);
        await _services.Product.AddVariantAsync(id, Variant("A", stock: null), default);
        var variant = Stored(id).Variants.Single();

        await _services.Product.UpdateAsync(
            id,
            Edit(
                Stored(id),
                type: FulfilmentType.InStock,
                variantStock: new Dictionary<Guid, int> { [variant.Id] = 0 }),
            default);

        Assert.Null(Stored(id).LeadTimeDays);
        Assert.Equal(0, variant.StockQuantity);
    }

    // --- Adding and editing a variant ---

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task AddVariant_WithoutSku_IsRejectedOnTheSkuField(string sku)
    {
        var id = await CreateProductAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Product.AddVariantAsync(id, Variant(sku), default));

        Assert.Contains("sku", error.Errors.Keys);
        Assert.Empty(Stored(id).Variants);
    }

    [Fact]
    public async Task AddVariant_WithASkuUsedByAnotherProduct_IsAConflictWithTheVariantCode()
    {
        var first = await CreateProductAsync("dragon");
        var second = await CreateProductAsync("phoenix");
        await _services.Product.AddVariantAsync(first, Variant("SKU-1"), default);

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => _services.Product.AddVariantAsync(second, Variant("SKU-1"), default));

        Assert.Equal("variant.sku_taken", error.Code);
        Assert.Empty(Stored(second).Variants);
    }

    [Fact]
    public async Task AddVariant_ToAnUnknownProduct_IsNotFoundWithTheProductCode()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.Product.AddVariantAsync(Guid.CreateVersion7(), Variant(), default));

        Assert.Equal("product.not_found", error.Code);
    }

    [Fact]
    public async Task AddVariant_SkuOf100CharactersIsAccepted_And101IsRejected()
    {
        var id = await CreateProductAsync();
        await _services.Product.AddVariantAsync(id, Variant(new string('A', 100)), default);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Product.AddVariantAsync(id, Variant(new string('B', 101)), default));
        Assert.Contains("sku", error.Errors.Keys);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    public async Task AddVariant_PriceMustNotBeNegative(long price, bool accepted)
    {
        var id = await CreateProductAsync();

        if (accepted)
        {
            await _services.Product.AddVariantAsync(id, Variant(price: price), default);
            Assert.Single(Stored(id).Variants);
        }
        else
        {
            var error = await Assert.ThrowsAsync<ValidationFailedException>(
                () => _services.Product.AddVariantAsync(id, Variant(price: price), default));
            Assert.Contains("price", error.Errors.Keys);
        }
    }

    [Fact]
    public async Task AddVariant_WeightMustNotBeNegative()
    {
        var id = await CreateProductAsync();
        await _services.Product.AddVariantAsync(id, Variant("A", weight: 0), default);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Product.AddVariantAsync(id, Variant("B", weight: -1), default));

        Assert.Contains("weightGrams", error.Errors.Keys);
    }

    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(100, 101, true)]
    [InlineData(100, 99, false)]
    [InlineData(0, 0, true)]
    public async Task AddVariant_CompareAtPriceMustNotBeLessThanThePrice(long price, long compareAt, bool accepted)
    {
        var id = await CreateProductAsync();

        if (accepted)
        {
            await _services.Product.AddVariantAsync(id, Variant(price: price, compareAtPrice: compareAt), default);
            Assert.Single(Stored(id).Variants);
        }
        else
        {
            var error = await Assert.ThrowsAsync<ValidationFailedException>(
                () => _services.Product.AddVariantAsync(id, Variant(price: price, compareAtPrice: compareAt), default));
            Assert.Contains("compareAtPrice", error.Errors.Keys);
        }
    }

    [Fact]
    public async Task AddVariant_NegativeCompareAtPriceIsRejected()
    {
        var id = await CreateProductAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Product.AddVariantAsync(id, Variant(price: 0, compareAtPrice: -1), default));

        Assert.Contains("compareAtPrice", error.Errors.Keys);
    }

    [Fact]
    public async Task AddVariant_ToAnInStockProduct_NeedsStock()
    {
        var id = await CreateProductAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Product.AddVariantAsync(id, Variant(stock: null), default));

        Assert.Contains("stockQuantity", error.Errors.Keys);
    }

    [Fact]
    public async Task AddVariant_ToAMadeToOrderProduct_MustHaveNoStock()
    {
        var id = await CreateProductAsync(type: FulfilmentType.MadeToOrder, leadTimeDays: 7);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Product.AddVariantAsync(id, Variant(stock: 3), default));
        Assert.Contains("stockQuantity", error.Errors.Keys);

        await _services.Product.AddVariantAsync(id, Variant(stock: null), default);
        Assert.Single(Stored(id).Variants);
    }

    [Fact]
    public async Task AddVariant_AttributesAreLimitedInCountAndLength()
    {
        var id = await CreateProductAsync();
        var ten = Enumerable.Range(0, 10).ToDictionary(i => $"k{i}", _ => "v");
        await _services.Product.AddVariantAsync(id, Variant("A", attributes: ten), default);

        var eleven = Enumerable.Range(0, 11).ToDictionary(i => $"k{i}", _ => "v");
        var tooMany = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Product.AddVariantAsync(id, Variant("B", attributes: eleven), default));
        var longKey = await Assert.ThrowsAsync<ValidationFailedException>(() => _services.Product.AddVariantAsync(
            id, Variant("C", attributes: new Dictionary<string, string> { [new string('k', 51)] = "v" }), default));
        var longValue = await Assert.ThrowsAsync<ValidationFailedException>(() => _services.Product.AddVariantAsync(
            id, Variant("D", attributes: new Dictionary<string, string> { ["k"] = new string('v', 101) }), default));
        var blankKey = await Assert.ThrowsAsync<ValidationFailedException>(() => _services.Product.AddVariantAsync(
            id, Variant("E", attributes: new Dictionary<string, string> { [" "] = "v" }), default));

        Assert.All([tooMany, longKey, longValue, blankKey], e => Assert.Contains("attributes", e.Errors.Keys));

        await _services.Product.AddVariantAsync(id, Variant("F", attributes: new Dictionary<string, string>
        {
            [new string('k', 50)] = new string('v', 100),
        }), default);
    }

    [Fact]
    public async Task UpdateVariant_ChangesTheFields_AndKeepsItsOwnSku()
    {
        var id = await CreateProductAsync();
        await _services.Product.AddVariantAsync(id, Variant("SKU-1"), default);
        var variant = Stored(id).Variants.Single();

        await _services.Product.UpdateVariantAsync(
            id, variant.Id, new UpdateVariant("SKU-1", "Red", 250, 20, 300, 9), default);

        Assert.Equal("Red", variant.Name);
        Assert.Equal(250, variant.Price);
        Assert.Equal(300, variant.CompareAtPrice);
        Assert.Equal(9, variant.StockQuantity);
    }

    [Fact]
    public async Task UpdateVariant_ToTheSkuOfAnotherVariant_IsAConflictWithTheVariantCode()
    {
        var id = await CreateProductAsync();
        await _services.Product.AddVariantAsync(id, Variant("A"), default);
        await _services.Product.AddVariantAsync(id, Variant("B"), default);
        var second = Stored(id).Variants.Single(v => v.Sku == "B");

        var error = await Assert.ThrowsAsync<ConflictException>(() => _services.Product.UpdateVariantAsync(
            id, second.Id, new UpdateVariant("A", "Blue", 100, 10, null, 5), default));

        Assert.Equal("variant.sku_taken", error.Code);
        Assert.Equal("B", second.Sku);
    }

    [Fact]
    public async Task UpdateVariant_OfAnUnknownVariant_IsNotFoundWithTheVariantCode()
    {
        var id = await CreateProductAsync();

        var error = await Assert.ThrowsAsync<NotFoundException>(() => _services.Product.UpdateVariantAsync(
            id, Guid.CreateVersion7(), new UpdateVariant("A", "Blue", 100, 10, null, 5), default));

        Assert.Equal("variant.not_found", error.Code);
    }

    [Fact]
    public async Task UpdateVariant_OfAnotherProduct_IsNotFound()
    {
        var first = await CreateProductAsync("dragon");
        var second = await CreateProductAsync("phoenix");
        await _services.Product.AddVariantAsync(first, Variant("A"), default);
        var variant = Stored(first).Variants.Single();

        var error = await Assert.ThrowsAsync<NotFoundException>(() => _services.Product.UpdateVariantAsync(
            second, variant.Id, new UpdateVariant("A", "Blue", 100, 10, null, 5), default));

        Assert.Equal("variant.not_found", error.Code);
    }

    [Fact]
    public async Task UpdateVariant_FollowsTheSameRulesAsAdding()
    {
        var id = await CreateProductAsync();
        await _services.Product.AddVariantAsync(id, Variant("A"), default);
        var variant = Stored(id).Variants.Single();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(() => _services.Product.UpdateVariantAsync(
            id, variant.Id, new UpdateVariant("A", "Blue", 100, 10, CompareAtPrice: 99, StockQuantity: null), default));

        Assert.Contains("compareAtPrice", error.Errors.Keys);
        Assert.Contains("stockQuantity", error.Errors.Keys);
    }

    // --- Activating, deactivating and ordering variants ---

    private async Task<Guid> PublishedWithVariantsAsync(params string[] skus)
    {
        var id = await CreateProductAsync();
        foreach (var (sku, index) in skus.Select((sku, index) => (sku, index)))
        {
            await _services.Product.AddVariantAsync(id, Variant(sku, sortOrder: index), default);
        }

        await _services.Product.SetStatusAsync(id, ProductStatus.Published, Stored(id).Version, default);
        return id;
    }

    private ProductVariant VariantOf(Guid productId, string sku) => Stored(productId).Variants.Single(v => v.Sku == sku);

    [Fact]
    public async Task Deactivating_ANonLastVariantOfAPublishedProduct_IsAccepted()
    {
        var id = await PublishedWithVariantsAsync("A", "B");

        await _services.Product.SetVariantActiveAsync(id, VariantOf(id, "A").Id, false, default);

        Assert.False(VariantOf(id, "A").IsActive);
        Assert.True(VariantOf(id, "B").IsActive);
    }

    [Fact]
    public async Task Deactivating_TheLastActiveVariantOfAPublishedProduct_IsAConflict()
    {
        var id = await PublishedWithVariantsAsync("A", "B");
        await _services.Product.SetVariantActiveAsync(id, VariantOf(id, "A").Id, false, default);

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => _services.Product.SetVariantActiveAsync(id, VariantOf(id, "B").Id, false, default));

        Assert.Equal("variant.last_active", error.Code);
        Assert.True(VariantOf(id, "B").IsActive);
    }

    [Fact]
    public async Task Deactivating_TheOnlyVariantOfADraftProduct_IsAccepted()
    {
        var id = await CreateProductAsync();
        await _services.Product.AddVariantAsync(id, Variant("A"), default);

        await _services.Product.SetVariantActiveAsync(id, VariantOf(id, "A").Id, false, default);

        Assert.False(VariantOf(id, "A").IsActive);
    }

    [Fact]
    public async Task Activating_AVariant_IsAccepted()
    {
        var id = await CreateProductAsync();
        await _services.Product.AddVariantAsync(id, Variant("A", isActive: false), default);

        await _services.Product.SetVariantActiveAsync(id, VariantOf(id, "A").Id, true, default);

        Assert.True(VariantOf(id, "A").IsActive);
    }

    [Fact]
    public async Task Deactivating_AnAlreadyInactiveVariantOfAPublishedProduct_IsAccepted()
    {
        var id = await PublishedWithVariantsAsync("A", "B");
        await _services.Product.SetVariantActiveAsync(id, VariantOf(id, "A").Id, false, default);

        await _services.Product.SetVariantActiveAsync(id, VariantOf(id, "A").Id, false, default);

        Assert.False(VariantOf(id, "A").IsActive);
    }

    private static string[] SkuOrder(Product product) =>
        product.Variants.OrderBy(v => v.SortOrder).ThenBy(v => v.Sku).Select(v => v.Sku).ToArray();

    [Fact]
    public async Task MovingAVariantDown_SwapsItWithTheNext_AndNumbersTheGroupFromZero()
    {
        var id = await CreateProductAsync();
        foreach (var sku in new[] { "A", "B", "C" })
        {
            await _services.Product.AddVariantAsync(id, Variant(sku, sortOrder: 5), default);
        }

        // Equal numbers are shown in id order, so the expected order is read before the move.
        var before = Stored(id).Variants.OrderBy(v => v.SortOrder).ThenBy(v => v.Id).Select(v => v.Sku).ToArray();

        await _services.Product.MoveVariantAsync(id, VariantOf(id, before[0]).Id, MoveDirection.Down, default);

        Assert.Equal([before[1], before[0], before[2]], Stored(id).Variants.OrderBy(v => v.SortOrder).Select(v => v.Sku));
        Assert.Equal([0, 1, 2], Stored(id).Variants.Select(v => v.SortOrder).Order());
    }

    [Fact]
    public async Task MovingAVariantUp_SwapsItWithThePrevious()
    {
        var id = await CreateProductAsync();
        foreach (var (sku, index) in new[] { "A", "B", "C" }.Select((s, i) => (s, i)))
        {
            await _services.Product.AddVariantAsync(id, Variant(sku, sortOrder: index), default);
        }

        await _services.Product.MoveVariantAsync(id, VariantOf(id, "C").Id, MoveDirection.Up, default);

        Assert.Equal(["A", "C", "B"], SkuOrder(Stored(id)));
    }

    [Fact]
    public async Task MovingTheFirstVariantUp_ChangesNothing()
    {
        var id = await CreateProductAsync();
        foreach (var (sku, index) in new[] { "A", "B" }.Select((s, i) => (s, i)))
        {
            await _services.Product.AddVariantAsync(id, Variant(sku, sortOrder: index), default);
        }

        await _services.Product.MoveVariantAsync(id, VariantOf(id, "A").Id, MoveDirection.Up, default);

        Assert.Equal(["A", "B"], SkuOrder(Stored(id)));
    }

    [Fact]
    public async Task MovingTheLastVariantDown_ChangesNothing()
    {
        var id = await CreateProductAsync();
        foreach (var (sku, index) in new[] { "A", "B" }.Select((s, i) => (s, i)))
        {
            await _services.Product.AddVariantAsync(id, Variant(sku, sortOrder: index), default);
        }

        await _services.Product.MoveVariantAsync(id, VariantOf(id, "B").Id, MoveDirection.Down, default);

        Assert.Equal(["A", "B"], SkuOrder(Stored(id)));
    }

    [Fact]
    public async Task MovingAnUnknownVariant_IsNotFoundWithTheVariantCode()
    {
        var id = await CreateProductAsync();

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.Product.MoveVariantAsync(id, Guid.CreateVersion7(), MoveDirection.Up, default));

        Assert.Equal("variant.not_found", error.Code);
    }

    // --- Status ---

    [Fact]
    public async Task Publishing_NeedsAnActiveVariant()
    {
        var id = await CreateProductAsync();
        await _services.Product.AddVariantAsync(id, Variant("A", isActive: false), default);

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => _services.Product.SetStatusAsync(id, ProductStatus.Published, Stored(id).Version, default));

        Assert.Equal("product.cannot_publish_without_active_variant", error.Code);
        Assert.Equal(ProductStatus.Draft, Stored(id).Status);
    }

    [Fact]
    public async Task Publishing_AProductWithoutVariants_IsAConflictWithTheSameCode()
    {
        var id = await CreateProductAsync();

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => _services.Product.SetStatusAsync(id, ProductStatus.Published, Stored(id).Version, default));

        Assert.Equal("product.cannot_publish_without_active_variant", error.Code);
    }

    [Fact]
    public async Task Publishing_AProductWithAnActiveVariant_IsAccepted_EvenWithInactiveOnesBesideIt()
    {
        var id = await CreateProductAsync();
        await _services.Product.AddVariantAsync(id, Variant("A", isActive: false), default);
        await _services.Product.AddVariantAsync(id, Variant("B"), default);

        await _services.Product.SetStatusAsync(id, ProductStatus.Published, Stored(id).Version, default);

        Assert.Equal(ProductStatus.Published, Stored(id).Status);
    }

    [Fact]
    public async Task APublishedProduct_CanBeArchived_AndBroughtBackToDraft()
    {
        var id = await PublishedWithVariantsAsync("A");

        await _services.Product.SetStatusAsync(id, ProductStatus.Archived, Stored(id).Version, default);
        Assert.Equal(ProductStatus.Archived, Stored(id).Status);

        await _services.Product.SetStatusAsync(id, ProductStatus.Draft, Stored(id).Version, default);
        Assert.Equal(ProductStatus.Draft, Stored(id).Status);
    }

    [Fact]
    public async Task ChangingTheStatus_WithAStaleVersion_IsAVersionConflict()
    {
        var id = await PublishedWithVariantsAsync("A");
        Stored(id).Version = 7;

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => _services.Product.SetStatusAsync(id, ProductStatus.Archived, 6, default));

        Assert.Equal("product.version_conflict", error.Code);
    }

    [Fact]
    public async Task ChangingTheStatus_OfAnUnknownProduct_IsNotFound()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.Product.SetStatusAsync(Guid.CreateVersion7(), ProductStatus.Archived, 0, default));

        Assert.Equal("product.not_found", error.Code);
    }
}
