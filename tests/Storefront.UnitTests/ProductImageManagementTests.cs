using System.Text;
using System.Text.RegularExpressions;
using Storefront.Application.Catalog;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

public sealed class ProductImageManagementTests
{
    private const int MaxBytes = 5 * 1024 * 1024;

    private readonly FakeRepositoryManager _repositories = new();
    private readonly FakeImageStorage _storage = new();
    private readonly ServiceManager _services;

    public ProductImageManagementTests() => _services = new ServiceManager(_repositories, _storage);

    private Product Product => _repositories.Products[0];

    private static byte[] Jpeg(int length = 16) => Padded([0xFF, 0xD8, 0xFF, 0xE0], length);

    private static byte[] Png() => Padded([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 16);

    private static byte[] Webp() => Padded("RIFF\0\0\0\0WEBP"u8.ToArray(), 16);

    private static byte[] Padded(byte[] start, int length)
    {
        var bytes = new byte[Math.Max(length, start.Length)];
        start.CopyTo(bytes, 0);
        return bytes;
    }

    private async Task<Guid> CreateProductAsync(string slug = "dragon", string sku = "SKU-1")
    {
        var categoryId = _repositories.Categories.FirstOrDefault()?.Id
            ?? await _services.Category.CreateAsync(new NewCategory("Figures", "figures"), default);
        var productId = await _services.Product.CreateAsync(
            new NewProduct(categoryId, "Dragon", slug, "Short", "Long", FulfilmentType.InStock),
            default);
        await _services.Product.AddVariantAsync(productId, new NewVariant(sku, "Blue", 100, 10, StockQuantity: 5), default);
        return productId;
    }

    private Task<Guid> UploadAsync(Guid productId, byte[]? content = null, string alt = "A dragon", Guid? variantId = null) =>
        _services.Product.UploadImageAsync(
            productId, new NewImageUpload(new MemoryStream(content ?? Jpeg()), alt, variantId), default);

    private static void AssertOrder(Product product, params Guid[] expected) =>
        Assert.Equal(expected, product.Images.OrderBy(i => i.SortOrder).Select(i => i.Id).ToArray());

    // ----- the file: type, size, count -----

    [Fact]
    public async Task Upload_StoresTheFile_AndTheRowPointsAtIt()
    {
        var productId = await CreateProductAsync();

        var imageId = await UploadAsync(productId);

        var image = Assert.Single(Product.Images);
        Assert.Equal(imageId, image.Id);
        Assert.Equal("image/jpeg", _storage.Files[image.StorageKey].ContentType);
        Assert.Equal(Jpeg(), _storage.Files[image.StorageKey].Content);
    }

    [Theory]
    [InlineData("jpeg", "jpg", "image/jpeg")]
    [InlineData("png", "png", "image/png")]
    [InlineData("webp", "webp", "image/webp")]
    public async Task Upload_AcceptsJpegPngAndWebp_AndNamesTheKeyByTheDetectedType(string kind, string extension, string contentType)
    {
        var productId = await CreateProductAsync();
        var bytes = kind switch { "jpeg" => Jpeg(), "png" => Png(), _ => Webp() };

        await UploadAsync(productId, bytes);

        var image = Assert.Single(Product.Images);
        Assert.EndsWith("." + extension, image.StorageKey, StringComparison.Ordinal);
        Assert.Equal(contentType, _storage.Files[image.StorageKey].ContentType);
    }

    [Theory]
    [InlineData("svg")]
    [InlineData("gif")]
    [InlineData("text")]
    [InlineData("exe")]
    [InlineData("riff-but-not-webp")]
    [InlineData("truncated-jpeg")]
    public async Task Upload_RejectsAnyOtherContent_WhateverTheFileIsCalled(string kind)
    {
        var productId = await CreateProductAsync();
        // The service never sees the name or the Content-Type of the upload, so a renamed ".jpg" is judged by its bytes.
        var bytes = kind switch
        {
            "svg" => Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"),
            "gif" => Encoding.ASCII.GetBytes("GIF89a......"),
            "text" => Encoding.UTF8.GetBytes("just text pretending to be a picture"),
            "exe" => [0x4D, 0x5A, 0x90, 0x00, 0x03],
            "riff-but-not-webp" => "RIFF\0\0\0\0WAVEfmt "u8.ToArray(),
            _ => [0xFF, 0xD8],
        };

        var error = await Assert.ThrowsAsync<FileRejectedException>(() => UploadAsync(productId, bytes));

        Assert.Equal("image.type_not_allowed", error.Code);
        Assert.False(error.TooLarge);
        Assert.Empty(Product.Images);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Upload_RejectsAnEmptyFile()
    {
        var productId = await CreateProductAsync();

        var error = await Assert.ThrowsAsync<FileRejectedException>(() => UploadAsync(productId, []));

        Assert.Equal("image.empty", error.Code);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Upload_AcceptsAFileOfExactlyTheMaximumSize()
    {
        var productId = await CreateProductAsync();

        await UploadAsync(productId, Jpeg(MaxBytes));

        Assert.Single(Product.Images);
    }

    [Fact]
    public async Task Upload_RejectsAFileOneByteOverTheMaximum_AndStoresNothing()
    {
        var productId = await CreateProductAsync();

        var error = await Assert.ThrowsAsync<FileRejectedException>(() => UploadAsync(productId, Jpeg(MaxBytes + 1)));

        Assert.Equal("image.too_large", error.Code);
        Assert.True(error.TooLarge);
        Assert.Empty(Product.Images);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Upload_AcceptsTheTwentiethImage_AndRejectsTheTwentyFirst()
    {
        var productId = await CreateProductAsync();
        for (var n = 0; n < 20; n++)
        {
            await UploadAsync(productId);
        }

        var error = await Assert.ThrowsAsync<ConflictException>(() => UploadAsync(productId));

        Assert.Equal("image.limit_reached", error.Code);
        Assert.Equal(20, Product.Images.Count);
        Assert.Equal(20, _storage.Files.Count);
    }

    // ----- the key -----

    [Fact]
    public async Task Upload_MakesTheKeyOnTheServer_FromTheProductIdAndAGuidOnly()
    {
        var productId = await CreateProductAsync();

        await UploadAsync(productId);

        var key = Product.Images.Single().StorageKey;
        Assert.Matches(new Regex($"^products/{productId:D}/[0-9a-f]{{32}}\\.jpg$"), key);
    }

    [Fact]
    public async Task Upload_GivesEveryImageItsOwnKey()
    {
        var productId = await CreateProductAsync();

        await UploadAsync(productId);
        await UploadAsync(productId);

        Assert.Equal(2, Product.Images.Select(i => i.StorageKey).Distinct().Count());
    }

    // ----- alt text and variant -----

    [Fact]
    public async Task Upload_TrimsTheAltText_AndAcceptsExactly300Characters()
    {
        var productId = await CreateProductAsync();

        await UploadAsync(productId, alt: "  " + new string('a', 300) + "  ");

        Assert.Equal(new string('a', 300), Product.Images.Single().AltText);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(0)]
    public async Task Upload_RejectsAnAltTextThatIsTooLongOrEmpty_AndStoresNothing(int length)
    {
        var productId = await CreateProductAsync();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => UploadAsync(productId, alt: new string('a', length)));

        Assert.Contains("altText", error.Errors.Keys);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Upload_RejectsAnAltTextOfOnlySpaces()
    {
        var productId = await CreateProductAsync();

        await Assert.ThrowsAsync<ValidationFailedException>(() => UploadAsync(productId, alt: "   "));
    }

    [Fact]
    public async Task Upload_RejectsAVariantOfAnotherProduct()
    {
        var productId = await CreateProductAsync("dragon", "SKU-1");
        var otherId = await CreateProductAsync("owl", "SKU-2");
        var foreignVariant = _repositories.Products.Single(p => p.Id == otherId).Variants.Single().Id;

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => UploadAsync(productId, variantId: foreignVariant));

        Assert.Contains("variantId", error.Errors.Keys);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Upload_AcceptsAVariantOfTheSameProduct()
    {
        var productId = await CreateProductAsync();
        var variantId = Product.Variants.Single().Id;

        await UploadAsync(productId, variantId: variantId);

        Assert.Equal(variantId, Product.Images.Single().VariantId);
    }

    [Fact]
    public async Task Upload_ToAnUnknownProduct_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => UploadAsync(Guid.CreateVersion7()));
        Assert.Empty(_storage.Files);
    }

    // ----- primary and order on upload -----

    [Fact]
    public async Task FirstUpload_IsPrimary_AndLaterOnesAreNot()
    {
        var productId = await CreateProductAsync();

        var first = await UploadAsync(productId);
        var second = await UploadAsync(productId);

        Assert.True(Product.Images.Single(i => i.Id == first).IsPrimary);
        Assert.False(Product.Images.Single(i => i.Id == second).IsPrimary);
        AssertOrder(Product, first, second);
        Assert.Equal([0, 1], Product.Images.OrderBy(i => i.SortOrder).Select(i => i.SortOrder));
    }

    // ----- failures leave no file behind -----

    [Fact]
    public async Task Upload_WhenTheDatabaseFailsAfterTheFileIsStored_RemovesTheFile()
    {
        var productId = await CreateProductAsync();
        _repositories.SaveFailure = new InvalidOperationException("database is down");

        await Assert.ThrowsAsync<InvalidOperationException>(() => UploadAsync(productId));

        Assert.Empty(_storage.Files);
        Assert.Single(_storage.DeletedKeys);
    }

    [Fact]
    public async Task Upload_WhenTheStorageFails_AddsNoRow()
    {
        var productId = await CreateProductAsync();
        var savesBefore = _repositories.SaveCount;
        _storage.SaveFailure = new IOException("storage is down");

        await Assert.ThrowsAsync<IOException>(() => UploadAsync(productId));

        Assert.Empty(Product.Images);
        Assert.Equal(savesBefore, _repositories.SaveCount);
    }

    // ----- update -----

    [Fact]
    public async Task Update_ChangesTheAltText_AndTheVariant()
    {
        var productId = await CreateProductAsync();
        var imageId = await UploadAsync(productId);
        var variantId = Product.Variants.Single().Id;

        await _services.Product.UpdateImageAsync(productId, imageId, new UpdateImage(" New alt ", variantId), default);

        var image = Product.Images.Single();
        Assert.Equal("New alt", image.AltText);
        Assert.Equal(variantId, image.VariantId);
    }

    [Fact]
    public async Task Update_CanTakeTheVariantAway()
    {
        var productId = await CreateProductAsync();
        var imageId = await UploadAsync(productId, variantId: Product.Variants.Single().Id);

        await _services.Product.UpdateImageAsync(productId, imageId, new UpdateImage("Alt", null), default);

        Assert.Null(Product.Images.Single().VariantId);
    }

    [Fact]
    public async Task Update_RejectsABadAltText_AndAForeignVariant_AndKeepsTheImage()
    {
        var productId = await CreateProductAsync("dragon", "SKU-1");
        var otherId = await CreateProductAsync("owl", "SKU-2");
        var foreignVariant = _repositories.Products.Single(p => p.Id == otherId).Variants.Single().Id;
        var imageId = await UploadAsync(productId, alt: "Keep me");

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Product.UpdateImageAsync(productId, imageId, new UpdateImage(new string('a', 301)), default));
        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Product.UpdateImageAsync(productId, imageId, new UpdateImage("Alt", foreignVariant), default));

        Assert.Equal("Keep me", _repositories.Products.Single(p => p.Id == productId).Images.Single().AltText);
    }

    [Fact]
    public async Task Update_OfAnImageOfAnotherProduct_IsNotFound()
    {
        var productId = await CreateProductAsync("dragon", "SKU-1");
        var otherId = await CreateProductAsync("owl", "SKU-2");
        var imageOfOther = await UploadAsync(otherId);

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.Product.UpdateImageAsync(productId, imageOfOther, new UpdateImage("Alt"), default));

        Assert.Equal("image.not_found", error.Code);
    }

    // ----- primary -----

    [Fact]
    public async Task SetPrimary_MakesTheOldPrimaryAnOrdinaryImage_InOneOperation()
    {
        var productId = await CreateProductAsync();
        var first = await UploadAsync(productId);
        var second = await UploadAsync(productId);

        await _services.Product.SetPrimaryImageAsync(productId, second, default);

        var primary = Assert.Single(Product.Images, i => i.IsPrimary);
        Assert.Equal(second, primary.Id);
        Assert.False(Product.Images.Single(i => i.Id == first).IsPrimary);
    }

    [Fact]
    public async Task SetPrimary_OnTheCurrentPrimary_ChangesNothing()
    {
        var productId = await CreateProductAsync();
        var first = await UploadAsync(productId);
        await UploadAsync(productId);
        var savesBefore = _repositories.SaveCount;

        await _services.Product.SetPrimaryImageAsync(productId, first, default);

        Assert.Equal(first, Assert.Single(Product.Images, i => i.IsPrimary).Id);
        Assert.Equal(savesBefore, _repositories.SaveCount);
    }

    [Fact]
    public async Task SetPrimary_OfAnUnknownImage_IsNotFound()
    {
        var productId = await CreateProductAsync();
        await UploadAsync(productId);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _services.Product.SetPrimaryImageAsync(productId, Guid.CreateVersion7(), default));
    }

    // ----- order -----

    [Fact]
    public async Task Move_SwapsNeighbours_AndNumbersTheImages0To2()
    {
        var productId = await CreateProductAsync();
        var a = await UploadAsync(productId);
        var b = await UploadAsync(productId);
        var c = await UploadAsync(productId);

        await _services.Product.MoveImageAsync(productId, c, MoveDirection.Up, default);

        AssertOrder(Product, a, c, b);
        Assert.Equal([0, 1, 2], Product.Images.Select(i => i.SortOrder).Order());
    }

    [Fact]
    public async Task Move_TheFirstUp_ChangesNothing()
    {
        var productId = await CreateProductAsync();
        var a = await UploadAsync(productId);
        var b = await UploadAsync(productId);

        await _services.Product.MoveImageAsync(productId, a, MoveDirection.Up, default);

        AssertOrder(Product, a, b);
    }

    [Fact]
    public async Task Move_TheLastDown_ChangesNothing()
    {
        var productId = await CreateProductAsync();
        var a = await UploadAsync(productId);
        var b = await UploadAsync(productId);

        await _services.Product.MoveImageAsync(productId, b, MoveDirection.Down, default);

        AssertOrder(Product, a, b);
    }

    [Fact]
    public async Task Move_RenumbersGapsAndEqualNumbers()
    {
        var productId = await CreateProductAsync();
        var a = await UploadAsync(productId);
        var b = await UploadAsync(productId);
        var c = await UploadAsync(productId);
        Product.Images.Single(i => i.Id == a).SortOrder = 5;
        Product.Images.Single(i => i.Id == b).SortOrder = 5;
        Product.Images.Single(i => i.Id == c).SortOrder = 9;

        await _services.Product.MoveImageAsync(productId, c, MoveDirection.Up, default);

        Assert.Equal([0, 1, 2], Product.Images.Select(i => i.SortOrder).Order());
        Assert.Equal(1, Product.Images.Single(i => i.Id == c).SortOrder);
    }

    // ----- delete -----

    [Fact]
    public async Task Delete_RemovesTheRow_AndTheFile()
    {
        var productId = await CreateProductAsync();
        var first = await UploadAsync(productId);
        var second = await UploadAsync(productId);
        var key = Product.Images.Single(i => i.Id == second).StorageKey;

        await _services.Product.DeleteImageAsync(productId, second, default);

        Assert.Equal(first, Assert.Single(Product.Images).Id);
        Assert.DoesNotContain(key, _storage.Files.Keys);
    }

    [Fact]
    public async Task Delete_OfThePrimary_MakesTheFirstInOrderPrimary_AndRenumbers()
    {
        var productId = await CreateProductAsync();
        var a = await UploadAsync(productId);
        var b = await UploadAsync(productId);
        var c = await UploadAsync(productId);

        await _services.Product.DeleteImageAsync(productId, a, default);

        var primary = Assert.Single(Product.Images, i => i.IsPrimary);
        Assert.Equal(b, primary.Id);
        AssertOrder(Product, b, c);
        Assert.Equal([0, 1], Product.Images.Select(i => i.SortOrder).Order());
    }

    [Fact]
    public async Task Delete_OfAnOrdinaryImage_KeepsThePrimary()
    {
        var productId = await CreateProductAsync();
        var a = await UploadAsync(productId);
        var b = await UploadAsync(productId);

        await _services.Product.DeleteImageAsync(productId, b, default);

        Assert.Equal(a, Assert.Single(Product.Images, i => i.IsPrimary).Id);
    }

    [Fact]
    public async Task Delete_OfTheOnlyImage_LeavesTheProductWithoutAnImage()
    {
        var productId = await CreateProductAsync();
        var only = await UploadAsync(productId);

        await _services.Product.DeleteImageAsync(productId, only, default);

        Assert.Empty(Product.Images);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Delete_WhenTheDatabaseFails_KeepsTheFile()
    {
        var productId = await CreateProductAsync();
        var imageId = await UploadAsync(productId);
        _repositories.SaveFailure = new InvalidOperationException("database is down");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _services.Product.DeleteImageAsync(productId, imageId, default));

        // A row that points at a missing file is the one thing that must never happen.
        Assert.Single(_storage.Files);
        Assert.Empty(_storage.DeletedKeys);
    }

    [Fact]
    public async Task Delete_OfAnUnknownImage_IsNotFound()
    {
        var productId = await CreateProductAsync();

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.Product.DeleteImageAsync(productId, Guid.CreateVersion7(), default));

        Assert.Equal("image.not_found", error.Code);
    }
}
