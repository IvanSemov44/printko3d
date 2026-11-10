using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storefront.IntegrationTests.Persistence;
using Storefront.IntegrationTests.ReadApi;
using Storefront.IntegrationTests.Storage;

namespace Storefront.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class AdminProductImagesApiTests(PostgresFixture postgres, S3Fixture s3) : IClassFixture<S3Fixture>
{
    private const string Base = "/api/v1/admin/products";
    private const int MaxBytes = 5 * 1024 * 1024;
    private static readonly string Id = "00000000-0000-0000-0000-000000000001";

    private static byte[] Jpeg(int length = 64) => Padded([0xFF, 0xD8, 0xFF, 0xE0], length);

    private static byte[] Png() => Padded([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 64);

    private static byte[] Padded(byte[] start, int length)
    {
        var bytes = new byte[Math.Max(length, start.Length)];
        start.CopyTo(bytes, 0);
        return bytes;
    }

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string CodeOf(JsonElement problem) => problem.GetProperty("code").GetString()!;

    private Task<AuthApiHost> CreateHostAsync() =>
        AuthApiHost.CreateAsync(postgres, extraSettings: s3.Settings());

    /// <summary>A product with one active variant, published, so the shop shows it. Returns its id and the id of the variant.</summary>
    private static async Task<(Guid ProductId, Guid VariantId, string Slug)> CreatePublishedProductAsync(
        AuthApiHost host, string cookie)
    {
        await using var context = host.Database.CreateContext();
        var category = ReadApiData.Category("figures");
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var slug = $"dragon-{Guid.NewGuid():N}"[..14];
        var created = await JsonOf(await host.SendAsync(HttpMethod.Post, Base, new
        {
            categoryId = category.Id,
            name = slug,
            slug,
            shortDescription = "Short",
            description = "Long",
            fulfilmentType = "InStock",
            leadTimeDays = (int?)null,
            seoTitle = (string?)null,
            seoDescription = (string?)null,
        }, cookie));
        var productId = created.GetProperty("id").GetGuid();
        var withVariant = await JsonOf(await host.SendAsync(HttpMethod.Post, $"{Base}/{productId}/variants", new
        {
            sku = $"SKU-{Guid.NewGuid():N}"[..12],
            name = "Blue",
            price = 1000L,
            weightGrams = 100,
            compareAtPrice = (long?)null,
            stockQuantity = 5,
            attributes = new Dictionary<string, string>(),
            isActive = true,
            sortOrder = 0,
        }, cookie));
        var status = await host.SendAsync(
            HttpMethod.Put,
            $"{Base}/{productId}/status",
            new { status = "Published", version = withVariant.GetProperty("version").GetUInt32() },
            cookie);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        return (productId, withVariant.GetProperty("variants")[0].GetProperty("id").GetGuid(), slug);
    }

    private static Task<HttpResponseMessage> UploadAsync(
        AuthApiHost host,
        string cookie,
        Guid productId,
        byte[] content,
        string fileName = "photo.jpg",
        string contentType = "image/jpeg",
        string? altText = "A dragon",
        Guid? variantId = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        // filename* (RFC 5987) carries any character, even quotes and emoji, that a plain filename cannot.
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"file\"",
            FileNameStar = fileName,
        };
        form.Add(file);
        if (altText is not null)
        {
            form.Add(new StringContent(altText, Encoding.UTF8), "altText");
        }

        if (variantId is { } variant)
        {
            form.Add(new StringContent(variant.ToString()), "variantId");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"{Base}/{productId}/images") { Content = form };
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return host.Client.SendAsync(request);
    }

    private static async Task<Guid> UploadOkAsync(AuthApiHost host, string cookie, Guid productId, string alt = "A dragon")
    {
        var response = await UploadAsync(host, cookie, productId, Jpeg(), altText: alt);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var images = (await JsonOf(response)).GetProperty("images").EnumerateArray().ToList();
        return images.Last().GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> AdminProductAsync(AuthApiHost host, string cookie, Guid productId) =>
        await JsonOf(await host.SendAsync(HttpMethod.Get, $"{Base}/{productId}", cookie: cookie));

    private static async Task<JsonElement> PublicProductAsync(AuthApiHost host, string slug) =>
        await JsonOf(await host.SendAsync(HttpMethod.Get, $"/api/v1/products/{slug}"));

    private static Guid[] IdsInOrder(JsonElement product) =>
        product.GetProperty("images").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();

    private static Guid[] PrimaryIds(JsonElement product) =>
        product.GetProperty("images").EnumerateArray()
            .Where(i => i.GetProperty("isPrimary").GetBoolean())
            .Select(i => i.GetProperty("id").GetGuid())
            .ToArray();

    // --- Who may call ---

    public static TheoryData<string, string> Addresses => new()
    {
        { "POST", $"{Base}/{Id}/images" },
        { "PUT", $"{Base}/{Id}/images/{Id}" },
        { "PUT", $"{Base}/{Id}/images/{Id}/primary" },
        { "POST", $"{Base}/{Id}/images/{Id}/move" },
        { "DELETE", $"{Base}/{Id}/images/{Id}" },
    };

    [Theory]
    [MemberData(nameof(Addresses))]
    public async Task EveryImageAddress_Answers401WithoutASignIn_AsProblemDetails(string method, string url)
    {
        await using var host = await CreateHostAsync();

        var response = await host.SendAsync(new HttpMethod(method), url, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Theory]
    [MemberData(nameof(Addresses))]
    public async Task EveryImageAddress_Answers403WithoutTheAdminRole_AsProblemDetails(string method, string url)
    {
        await using var host = await CreateHostAsync();
        await host.CreateUserWithoutRoleAsync("viewer@example.test", "another made-up password");
        var cookie = await host.LoginAsync("viewer@example.test", "another made-up password");

        var response = await host.SendAsync(new HttpMethod(method), url, new { }, cookie);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    // --- Upload ---

    [Fact]
    public async Task Upload_AddsThePrimaryImage_ReturnsTheProduct_AndStoresTheFileInTheBucket()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);

        var response = await UploadAsync(host, cookie, productId, Jpeg());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"{Base}/{productId}", response.Headers.Location!.AbsolutePath);
        var image = Assert.Single((await JsonOf(response)).GetProperty("images").EnumerateArray());
        Assert.True(image.GetProperty("isPrimary").GetBoolean());
        Assert.Equal(0, image.GetProperty("sortOrder").GetInt32());
        Assert.Equal("A dragon", image.GetProperty("altText").GetString());
        var key = image.GetProperty("storageKey").GetString()!;
        Assert.Matches($"^products/{productId:D}/[0-9a-f]{{32}}\\.jpg$", key);
        Assert.Contains(key, await s3.KeysAsync($"products/{productId:D}/"));
    }

    [Fact]
    public async Task Upload_OfAFileNamedJpgWithOtherContent_Is400_AndLeavesNothingInTheBucket()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);

        var response = await UploadAsync(
            host, cookie, productId, Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"), "photo.jpg");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
        Assert.Equal("image.type_not_allowed", CodeOf(await JsonOf(response)));
        Assert.Empty(await s3.KeysAsync($"products/{productId:D}/"));
        Assert.Empty((await AdminProductAsync(host, cookie, productId)).GetProperty("images").EnumerateArray());
    }

    [Fact]
    public async Task Upload_WithAForgedContentTypeHeader_IsJudgedByTheBytes()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);

        var response = await UploadAsync(
            host, cookie, productId, Encoding.UTF8.GetBytes("not a picture at all"), "photo.png", "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("image.type_not_allowed", CodeOf(await JsonOf(response)));
    }

    [Fact]
    public async Task Upload_OfARealPng_WithAWrongNameAndHeader_IsAccepted_AsPng()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);

        var response = await UploadAsync(host, cookie, productId, Png(), "shell.php", "text/plain");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var key = (await JsonOf(response)).GetProperty("images")[0].GetProperty("storageKey").GetString()!;
        Assert.EndsWith(".png", key, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../../etc/passwd.jpg")]
    [InlineData("..\\..\\windows\\system32\\x.jpg")]
    [InlineData("фото 🐉 \"quoted\" %2e%2e%2f.jpg")]
    public async Task Upload_NeverPutsTheClientFileNameIntoTheKey(string fileName)
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);

        var response = await UploadAsync(host, cookie, productId, Jpeg(), fileName);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var key = (await JsonOf(response)).GetProperty("images")[0].GetProperty("storageKey").GetString()!;
        Assert.Matches($"^products/{productId:D}/[0-9a-f]{{32}}\\.jpg$", key);
        Assert.Equal([key], await s3.KeysAsync($"products/{productId:D}/"));
    }

    [Fact]
    public async Task Upload_OfExactlyTheMaximumSize_Is201_AndOneByteMoreIs413WithACode()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);

        var atLimit = await UploadAsync(host, cookie, productId, Jpeg(MaxBytes));
        var overLimit = await UploadAsync(host, cookie, productId, Jpeg(MaxBytes + 1));

        Assert.Equal(HttpStatusCode.Created, atLimit.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, overLimit.StatusCode);
        Assert.True(overLimit.IsProblemJson());
        Assert.Equal("image.too_large", CodeOf(await JsonOf(overLimit)));
        Assert.Single(await s3.KeysAsync($"products/{productId:D}/"));
    }

    [Fact]
    public async Task Upload_OfAnEmptyFile_Is400WithACode()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);

        var response = await UploadAsync(host, cookie, productId, []);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("image.empty", CodeOf(await JsonOf(response)));
    }

    [Fact]
    public async Task Upload_WithoutAltText_Is400_NamingTheField()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);

        var response = await UploadAsync(host, cookie, productId, Jpeg(), altText: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
        Assert.Equal("validation.failed", CodeOf(await JsonOf(response)));
        Assert.Empty(await s3.KeysAsync($"products/{productId:D}/"));
    }

    [Fact]
    public async Task Upload_WithAVariantOfAnotherProduct_Is400_NamingTheField()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);

        var response = await UploadAsync(host, cookie, productId, Jpeg(), variantId: Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonOf(response)).GetProperty("errors").TryGetProperty("variantId", out _));
        Assert.Empty(await s3.KeysAsync($"products/{productId:D}/"));
    }

    [Fact]
    public async Task Upload_ToAnUnknownProduct_Is404()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();

        var response = await UploadAsync(host, cookie, Guid.CreateVersion7(), Jpeg());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("product.not_found", CodeOf(await JsonOf(response)));
    }

    // --- Change, order, primary, delete: and the shop sees it ---

    [Fact]
    public async Task Move_ChangesTheOrder_ForTheAdminAndForTheShop()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, slug) = await CreatePublishedProductAsync(host, cookie);
        var a = await UploadOkAsync(host, cookie, productId, "A");
        var b = await UploadOkAsync(host, cookie, productId, "B");
        var c = await UploadOkAsync(host, cookie, productId, "C");

        var response = await host.SendAsync(
            HttpMethod.Post, $"{Base}/{productId}/images/{c}/move", new { direction = "Up" }, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([a, c, b], IdsInOrder(await JsonOf(response)));
        Assert.Equal([a, c, b], IdsInOrder(await PublicProductAsync(host, slug)));
    }

    [Fact]
    public async Task Move_OfTheFirstUp_ChangesNothing()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);
        var a = await UploadOkAsync(host, cookie, productId);
        var b = await UploadOkAsync(host, cookie, productId);

        var response = await host.SendAsync(
            HttpMethod.Post, $"{Base}/{productId}/images/{a}/move", new { direction = "Up" }, cookie);

        Assert.Equal([a, b], IdsInOrder(await JsonOf(response)));
    }

    [Fact]
    public async Task SetPrimary_MovesThePrimaryFlag_ForTheAdminAndForTheShop()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, slug) = await CreatePublishedProductAsync(host, cookie);
        await UploadOkAsync(host, cookie, productId);
        var second = await UploadOkAsync(host, cookie, productId);

        var response = await host.SendAsync(HttpMethod.Put, $"{Base}/{productId}/images/{second}/primary", cookie: cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([second], PrimaryIds(await JsonOf(response)));
        Assert.Equal([second], PrimaryIds(await PublicProductAsync(host, slug)));
    }

    [Fact]
    public async Task Update_ChangesAltTextAndVariant_ForTheAdminAndForTheShop()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, variantId, slug) = await CreatePublishedProductAsync(host, cookie);
        var imageId = await UploadOkAsync(host, cookie, productId);

        var response = await host.SendAsync(
            HttpMethod.Put, $"{Base}/{productId}/images/{imageId}", new { altText = "  Blue dragon  ", variantId }, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shopImage = (await PublicProductAsync(host, slug)).GetProperty("images")[0];
        Assert.Equal("Blue dragon", shopImage.GetProperty("altText").GetString());
        Assert.Equal(variantId, shopImage.GetProperty("variantId").GetGuid());
    }

    [Fact]
    public async Task Update_WithATooLongAltText_Is400_AndKeepsTheImage()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);
        var imageId = await UploadOkAsync(host, cookie, productId, "Keep me");

        var response = await host.SendAsync(
            HttpMethod.Put, $"{Base}/{productId}/images/{imageId}", new { altText = new string('a', 301) }, cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "Keep me",
            (await AdminProductAsync(host, cookie, productId)).GetProperty("images")[0].GetProperty("altText").GetString());
    }

    [Fact]
    public async Task Delete_RemovesTheRowAndTheFile_AndTheNextImageBecomesPrimary_ForTheShopToo()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, slug) = await CreatePublishedProductAsync(host, cookie);
        var first = await UploadOkAsync(host, cookie, productId);
        var second = await UploadOkAsync(host, cookie, productId);
        var firstKey = (await AdminProductAsync(host, cookie, productId)).GetProperty("images")[0]
            .GetProperty("storageKey").GetString()!;

        var response = await host.SendAsync(HttpMethod.Delete, $"{Base}/{productId}/images/{first}", cookie: cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shop = await PublicProductAsync(host, slug);
        Assert.Equal([second], IdsInOrder(shop));
        Assert.Equal([second], PrimaryIds(shop));
        Assert.Equal(0, shop.GetProperty("images")[0].GetProperty("sortOrder").GetInt32());
        Assert.DoesNotContain(firstKey, await s3.KeysAsync($"products/{productId:D}/"));
    }

    [Fact]
    public async Task Delete_OfAnUnknownImage_Is404WithACode()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);

        var response = await host.SendAsync(
            HttpMethod.Delete, $"{Base}/{productId}/images/{Guid.CreateVersion7()}", cookie: cookie);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("image.not_found", CodeOf(await JsonOf(response)));
    }

    [Fact]
    public async Task TheTwentyFirstImage_Is409WithACode_AndLeavesNoFile()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);
        for (var n = 0; n < 20; n++)
        {
            await UploadOkAsync(host, cookie, productId);
        }

        var response = await UploadAsync(host, cookie, productId, Jpeg());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("image.limit_reached", CodeOf(await JsonOf(response)));
        Assert.Equal(20, (await s3.KeysAsync($"products/{productId:D}/")).Count);
    }

    // --- Two requests at the same moment ---

    [Fact]
    public async Task ChoosingThePrimaryImageAtTheSameMoment_NeverLeavesTwoPrimaries_AndNeverAnswers500()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);
        await UploadOkAsync(host, cookie, productId);
        var others = new[]
        {
            await UploadOkAsync(host, cookie, productId),
            await UploadOkAsync(host, cookie, productId),
            await UploadOkAsync(host, cookie, productId),
        };

        var responses = await Task.WhenAll(others.Select(id =>
            host.SendAsync(HttpMethod.Put, $"{Base}/{productId}/images/{id}/primary", cookie: cookie)));

        Assert.All(responses, r => Assert.True(
            r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, $"Unexpected {(int)r.StatusCode}."));
        await using var context = host.Database.CreateContext();
        Assert.True(await context.ProductImages.CountAsync(i => i.ProductId == productId && i.IsPrimary) <= 1);
    }

    [Fact]
    public async Task UploadingTheFirstImagesAtTheSameMoment_NeverAnswers500_LeavesOnePrimary_AndNoFileWithoutARow()
    {
        await using var host = await CreateHostAsync();
        var cookie = await host.LoginAsync();
        var (productId, _, _) = await CreatePublishedProductAsync(host, cookie);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => UploadAsync(host, cookie, productId, Jpeg())));

        Assert.All(responses, r => Assert.True(
            r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict, $"Unexpected {(int)r.StatusCode}."));
        await using var context = host.Database.CreateContext();
        var rows = await context.ProductImages.Where(i => i.ProductId == productId).ToListAsync();
        Assert.Equal(1, rows.Count(i => i.IsPrimary));
        // The requests that lost the race stored a file first. Their files must be gone: one file for every row.
        Assert.Equal(rows.Select(i => i.StorageKey).Order(), await s3.KeysAsync($"products/{productId:D}/"));
    }
}
