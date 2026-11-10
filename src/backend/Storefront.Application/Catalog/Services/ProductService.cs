using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

internal sealed class ProductService(IRepositoryManager repositories, IImageStorage storage) : IProductService
{
    public async Task<Guid> CreateAsync(NewProduct input, CancellationToken cancellationToken)
    {
        var leadTimeDays = ProductRules.Validate(
            input.Name,
            input.Slug,
            input.ShortDescription,
            input.FulfilmentType,
            input.LeadTimeDays,
            input.SeoTitle,
            input.SeoDescription);
        await EnsureCategoryExistsAsync(input.CategoryId, cancellationToken);
        await EnsureSlugIsFreeAsync(input.Slug, exceptProductId: null, cancellationToken);

        var product = new Product
        {
            CategoryId = input.CategoryId,
            Name = input.Name.Trim(),
            Slug = input.Slug,
            ShortDescription = input.ShortDescription,
            Description = input.Description,
            FulfilmentType = input.FulfilmentType,
            LeadTimeDays = leadTimeDays,
            SeoTitle = input.SeoTitle,
            SeoDescription = input.SeoDescription,
        };
        repositories.Product.Create(product);
        await repositories.SaveAsync(cancellationToken);
        return product.Id;
    }

    public async Task UpdateAsync(Guid productId, UpdateProduct input, CancellationToken cancellationToken)
    {
        var leadTimeDays = ProductRules.Validate(
            input.Name,
            input.Slug,
            input.ShortDescription,
            input.FulfilmentType,
            input.LeadTimeDays,
            input.SeoTitle,
            input.SeoDescription);
        var product = await GetTrackedProductAsync(productId, cancellationToken);
        repositories.Product.ExpectVersion(product, input.Version);

        if (input.CategoryId != product.CategoryId)
        {
            await EnsureCategoryExistsAsync(input.CategoryId, cancellationToken);
        }

        if (input.Slug != product.Slug)
        {
            await EnsureSlugIsFreeAsync(input.Slug, productId, cancellationToken);
        }

        // Worked out before anything is changed, so a refused request leaves the product as it was.
        var stock = ProductRules.StockAfterTypeChange(product, input);

        product.CategoryId = input.CategoryId;
        product.Name = input.Name.Trim();
        product.Slug = input.Slug;
        product.ShortDescription = input.ShortDescription;
        product.Description = input.Description;
        product.FulfilmentType = input.FulfilmentType;
        product.LeadTimeDays = leadTimeDays;
        product.SeoTitle = input.SeoTitle;
        product.SeoDescription = input.SeoDescription;
        foreach (var variant in product.Variants)
        {
            if (stock.TryGetValue(variant.Id, out var quantity))
            {
                variant.StockQuantity = quantity;
            }
        }

        await repositories.SaveAsync(cancellationToken);
    }

    public async Task SetStatusAsync(Guid productId, ProductStatus status, uint version, CancellationToken cancellationToken)
    {
        var product = await GetTrackedProductAsync(productId, cancellationToken);
        repositories.Product.ExpectVersion(product, version);
        ApplyStatus(product, status);
        await repositories.SaveAsync(cancellationToken);
    }

    public Task PublishAsync(Guid productId, CancellationToken cancellationToken) =>
        SetStatusWithoutVersionAsync(productId, ProductStatus.Published, cancellationToken);

    public Task ArchiveAsync(Guid productId, CancellationToken cancellationToken) =>
        SetStatusWithoutVersionAsync(productId, ProductStatus.Archived, cancellationToken);

    public async Task<Guid> AddVariantAsync(Guid productId, NewVariant input, CancellationToken cancellationToken)
    {
        var product = await GetTrackedProductAsync(productId, cancellationToken);
        var fields = VariantFields.From(input);
        var errors = VariantRules.Validate(product.FulfilmentType, fields);
        if (input.SortOrder < 0)
        {
            errors.Add("sortOrder", "Sort order must not be negative.");
        }

        errors.ThrowIfAny("The variant is not valid.");
        await EnsureSkuIsFreeAsync(fields.TrimmedSku, exceptVariantId: null, cancellationToken);

        var variant = new ProductVariant { ProductId = product.Id, IsActive = input.IsActive, SortOrder = input.SortOrder };
        fields.ApplyTo(variant);
        product.Variants.Add(variant);
        await repositories.SaveAsync(cancellationToken);
        return variant.Id;
    }

    public async Task UpdateVariantAsync(
        Guid productId,
        Guid variantId,
        UpdateVariant input,
        CancellationToken cancellationToken)
    {
        var product = await GetTrackedProductAsync(productId, cancellationToken);
        var variant = FindVariant(product, variantId);
        var fields = VariantFields.From(input);
        VariantRules.Validate(product.FulfilmentType, fields).ThrowIfAny("The variant is not valid.");
        if (fields.TrimmedSku != variant.Sku)
        {
            await EnsureSkuIsFreeAsync(fields.TrimmedSku, variantId, cancellationToken);
        }

        fields.ApplyTo(variant);
        await repositories.SaveAsync(cancellationToken);
    }

    public async Task SetVariantActiveAsync(
        Guid productId,
        Guid variantId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var product = await GetTrackedProductAsync(productId, cancellationToken);
        var variant = FindVariant(product, variantId);
        if (!isActive
            && variant.IsActive
            && product.Status == ProductStatus.Published
            && product.Variants.Count(v => v.IsActive) == 1)
        {
            throw new ConflictException(
                "The last active variant of a published product cannot be deactivated. Archive the product or activate another variant first.",
                "variant.last_active");
        }

        variant.IsActive = isActive;
        await repositories.SaveAsync(cancellationToken);
    }

    public async Task MoveVariantAsync(
        Guid productId,
        Guid variantId,
        MoveDirection direction,
        CancellationToken cancellationToken)
    {
        var product = await GetTrackedProductAsync(productId, cancellationToken);
        FindVariant(product, variantId);

        var ordered = product.Variants.OrderBy(v => v.SortOrder).ThenBy(v => v.Id).ToList();
        var index = ordered.FindIndex(v => v.Id == variantId);
        var target = direction == MoveDirection.Up ? index - 1 : index + 1;
        if (target >= 0 && target < ordered.Count)
        {
            (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        }

        // Rewrite the whole group as 0, 1, 2...: swapping two equal sortOrders would otherwise change nothing.
        // Rows whose number stays the same are not touched, so they are not written.
        for (var position = 0; position < ordered.Count; position++)
        {
            if (ordered[position].SortOrder != position)
            {
                ordered[position].SortOrder = position;
            }
        }

        await repositories.SaveAsync(cancellationToken);
    }

    public async Task<Guid> AddImageAsync(Guid productId, NewImage input, CancellationToken cancellationToken)
    {
        Guard.NotBlank(input.StorageKey, nameof(input.StorageKey));
        Guard.NotBlank(input.AltText, nameof(input.AltText));

        var product = await GetTrackedProductAsync(productId, cancellationToken);
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
        var product = await GetTrackedProductAsync(productId, cancellationToken);
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
        var product = await GetTrackedProductAsync(productId, cancellationToken);
        var image = FindImage(product, imageId);
        var altText = input.AltText.Trim();
        ValidateAltTextAndVariant(product, altText, input.VariantId);

        image.AltText = altText;
        image.VariantId = input.VariantId;
        await repositories.SaveAsync(cancellationToken);
    }

    public async Task SetPrimaryImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken)
    {
        var product = await GetTrackedProductAsync(productId, cancellationToken);
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
        var product = await GetTrackedProductAsync(productId, cancellationToken);
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
        var product = await GetTrackedProductAsync(productId, cancellationToken);
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

    public Task<PagedResult<ProductAdminListItemDto>> GetListAsync(
        ProductAdminListQuery query,
        CancellationToken cancellationToken) =>
        repositories.Catalog.GetAdminProductsAsync(query, cancellationToken);

    public async Task<ProductAdminDto> GetAsync(Guid productId, CancellationToken cancellationToken) =>
        await repositories.Catalog.GetAdminProductAsync(productId, cancellationToken)
            ?? throw ProductNotFound(productId);

    private static NotFoundException ProductNotFound(Guid id) =>
        new($"Product {id} was not found.", "product.not_found");

    private async Task<Product> GetTrackedProductAsync(Guid productId, CancellationToken cancellationToken) =>
        await repositories.Product.GetByIdAsync(productId, trackChanges: true, cancellationToken)
            ?? throw ProductNotFound(productId);

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

    private static ProductVariant FindVariant(Product product, Guid variantId) =>
        product.Variants.SingleOrDefault(v => v.Id == variantId)
            ?? throw new NotFoundException($"Variant {variantId} was not found.", "variant.not_found");

    private async Task SetStatusWithoutVersionAsync(
        Guid productId,
        ProductStatus status,
        CancellationToken cancellationToken)
    {
        var product = await GetTrackedProductAsync(productId, cancellationToken);
        ApplyStatus(product, status);
        await repositories.SaveAsync(cancellationToken);
    }

    private static void ApplyStatus(Product product, ProductStatus status)
    {
        if (status == ProductStatus.Published && !product.Variants.Any(v => v.IsActive))
        {
            throw new ConflictException(
                "A product needs at least one active variant to be published.",
                "product.cannot_publish_without_active_variant");
        }

        product.Status = status;
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        _ = await repositories.Category.GetByIdAsync(categoryId, trackChanges: false, cancellationToken)
            ?? throw new NotFoundException($"Category {categoryId} was not found.", "product.category_not_found");
    }

    private async Task EnsureSlugIsFreeAsync(string slug, Guid? exceptProductId, CancellationToken cancellationToken)
    {
        if (await repositories.Product.ExistsBySlugAsync(slug, exceptProductId, cancellationToken))
        {
            throw new ConflictException($"The slug '{slug}' is already used.", "product.slug_taken");
        }
    }

    private async Task EnsureSkuIsFreeAsync(string sku, Guid? exceptVariantId, CancellationToken cancellationToken)
    {
        if (await repositories.Product.ExistsBySkuAsync(sku, exceptVariantId, cancellationToken))
        {
            throw new ConflictException($"The SKU '{sku}' is already used.", "variant.sku_taken");
        }
    }
}
