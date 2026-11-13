using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

internal sealed class ProductImageService(IRepositoryManager repositories, IImageStorage storage)
{
    public async Task<Guid> AddImageAsync(Guid productId, NewImage input, CancellationToken cancellationToken)
    {
        Guard.NotBlank(input.StorageKey, nameof(input.StorageKey));
        Guard.NotBlank(input.AltText, nameof(input.AltText));

        var product = await TrackedProducts.GetAsync(repositories, productId, cancellationToken);
        if (input.VariantId is { } variantId && product.Variants.All(v => v.Id != variantId))
        {
            throw new ValidationFailedException("The variant does not belong to this product.");
        }

        var makePrimary = input.IsPrimary || product.Images.Count == 0;
        var currentPrimary = product.Images.SingleOrDefault(i => i.IsPrimary);
        if (makePrimary && currentPrimary is not null)
        {
            // Saved separately: the database allows one primary image per product at any moment.
            currentPrimary.IsPrimary = false;
            await repositories.SaveAsync(cancellationToken);
        }

        var image = new ProductImage
        {
            ProductId = product.Id,
            VariantId = input.VariantId,
            StorageKey = input.StorageKey,
            AltText = input.AltText,
            SortOrder = input.SortOrder,
            IsPrimary = makePrimary,
        };
        product.Images.Add(image);
        await repositories.SaveAsync(cancellationToken);
        return image.Id;
    }

    public async Task<Guid> UploadImageAsync(Guid productId, NewImageUpload input, CancellationToken cancellationToken)
    {
        var product = await TrackedProducts.GetAsync(repositories, productId, cancellationToken);
        var altText = input.AltText.Trim();
        ValidateAltTextAndVariant(product, altText, input.VariantId);
        if (product.Images.Count >= ImageRules.MaxImagesPerProduct)
        {
            throw new ConflictException(
                $"A product can have at most {ImageRules.MaxImagesPerProduct} images.", ImageRules.LimitReachedCode);
        }

        var bytes = await ImageRules.ReadLimitedAsync(input.Content, cancellationToken);
        var type = ImageRules.CheckFile(bytes);

        var key = ImageRules.NewKey(product.Id, type);
        await using (var file = new MemoryStream(bytes, writable: false))
        {
            await storage.SaveAsync(key, file, type.ContentType, cancellationToken);
        }

        var image = new ProductImage
        {
            ProductId = product.Id,
            VariantId = input.VariantId,
            StorageKey = key,
            AltText = altText,
            SortOrder = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.SortOrder) + 1,
            IsPrimary = product.Images.Count == 0,
        };
        product.Images.Add(image);
        try
        {
            await repositories.SaveAsync(cancellationToken);
        }
        catch
        {
            // The file has no row, so nobody could ever delete it later: remove it now.
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        return image.Id;
    }

    public async Task UpdateImageAsync(Guid productId, Guid imageId, UpdateImage input, CancellationToken cancellationToken)
    {
        var product = await TrackedProducts.GetAsync(repositories, productId, cancellationToken);
        var image = FindImage(product, imageId);
        var altText = input.AltText.Trim();
        ValidateAltTextAndVariant(product, altText, input.VariantId);

        image.AltText = altText;
        image.VariantId = input.VariantId;
        await repositories.SaveAsync(cancellationToken);
    }

    public async Task SetPrimaryImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken)
    {
        var product = await TrackedProducts.GetAsync(repositories, productId, cancellationToken);
        var image = FindImage(product, imageId);
        if (image.IsPrimary)
        {
            return;
        }

        // Two saves: the database allows one primary image per product at any moment, so the old one is cleared first.
        foreach (var current in product.Images.Where(i => i.IsPrimary))
        {
            current.IsPrimary = false;
        }

        await repositories.SaveAsync(cancellationToken);
        image.IsPrimary = true;
        await repositories.SaveAsync(cancellationToken);
    }

    public async Task MoveImageAsync(
        Guid productId,
        Guid imageId,
        MoveDirection direction,
        CancellationToken cancellationToken)
    {
        var product = await TrackedProducts.GetAsync(repositories, productId, cancellationToken);
        FindImage(product, imageId);

        var ordered = InOrder(product);
        var index = ordered.FindIndex(i => i.Id == imageId);
        var target = direction == MoveDirection.Up ? index - 1 : index + 1;
        if (target >= 0 && target < ordered.Count)
        {
            (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        }

        Renumber(ordered);
        await repositories.SaveAsync(cancellationToken);
    }

    public async Task DeleteImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken)
    {
        var product = await TrackedProducts.GetAsync(repositories, productId, cancellationToken);
        var image = FindImage(product, imageId);
        var wasPrimary = image.IsPrimary;

        // The row goes first. If the file cannot be removed afterwards, a file without a row is left (and logged),
        // never a row that points at a missing file.
        product.Images.Remove(image);
        repositories.Product.RemoveImage(image);
        await repositories.SaveAsync(cancellationToken);

        var remaining = InOrder(product);
        if (wasPrimary && remaining.Count > 0)
        {
            remaining[0].IsPrimary = true;
        }

        Renumber(remaining);
        await repositories.SaveAsync(cancellationToken);

        await storage.DeleteAsync(image.StorageKey, cancellationToken);
    }

    private static ProductImage FindImage(Product product, Guid imageId) =>
        product.Images.SingleOrDefault(i => i.Id == imageId)
            ?? throw new NotFoundException($"Image {imageId} was not found.", ImageRules.NotFoundCode);

    private static List<ProductImage> InOrder(Product product) =>
        product.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();

    // Rewrites the whole group as 0, 1, 2...; rows whose number stays the same are not touched.
    private static void Renumber(List<ProductImage> ordered)
    {
        for (var position = 0; position < ordered.Count; position++)
        {
            if (ordered[position].SortOrder != position)
            {
                ordered[position].SortOrder = position;
            }
        }
    }

    private static void ValidateAltTextAndVariant(Product product, string altText, Guid? variantId)
    {
        var errors = new FieldErrors();
        if (ImageRules.AltTextError(altText) is { } altError)
        {
            errors.Add("altText", altError);
        }

        if (variantId is { } id && product.Variants.All(v => v.Id != id))
        {
            errors.Add("variantId", "The variant does not belong to this product.");
        }

        errors.ThrowIfAny("The image is not valid.");
    }
}
