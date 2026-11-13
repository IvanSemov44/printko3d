using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

// The product's own rules live here. Variants and images are separate services; this class hands those calls on,
// so the callers of IProductService do not change.
internal sealed class ProductService(IRepositoryManager repositories, IImageStorage storage) : IProductService
{
    private readonly ProductVariantService _variants = new(repositories);
    private readonly ProductImageService _images = new(repositories, storage);

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

    public Task<Guid> AddVariantAsync(Guid productId, NewVariant input, CancellationToken cancellationToken) =>
        _variants.AddVariantAsync(productId, input, cancellationToken);

    public Task UpdateVariantAsync(
        Guid productId,
        Guid variantId,
        UpdateVariant input,
        CancellationToken cancellationToken) =>
        _variants.UpdateVariantAsync(productId, variantId, input, cancellationToken);

    public Task SetVariantActiveAsync(
        Guid productId,
        Guid variantId,
        bool isActive,
        CancellationToken cancellationToken) =>
        _variants.SetVariantActiveAsync(productId, variantId, isActive, cancellationToken);

    public Task MoveVariantAsync(
        Guid productId,
        Guid variantId,
        MoveDirection direction,
        CancellationToken cancellationToken) =>
        _variants.MoveVariantAsync(productId, variantId, direction, cancellationToken);

    public Task<Guid> AddImageAsync(Guid productId, NewImage input, CancellationToken cancellationToken) =>
        _images.AddImageAsync(productId, input, cancellationToken);

    public Task<Guid> UploadImageAsync(Guid productId, NewImageUpload input, CancellationToken cancellationToken) =>
        _images.UploadImageAsync(productId, input, cancellationToken);

    public Task UpdateImageAsync(Guid productId, Guid imageId, UpdateImage input, CancellationToken cancellationToken) =>
        _images.UpdateImageAsync(productId, imageId, input, cancellationToken);

    public Task SetPrimaryImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken) =>
        _images.SetPrimaryImageAsync(productId, imageId, cancellationToken);

    public Task MoveImageAsync(
        Guid productId,
        Guid imageId,
        MoveDirection direction,
        CancellationToken cancellationToken) =>
        _images.MoveImageAsync(productId, imageId, direction, cancellationToken);

    public Task DeleteImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken) =>
        _images.DeleteImageAsync(productId, imageId, cancellationToken);

    public Task<PagedResult<ProductAdminListItemDto>> GetListAsync(
        ProductAdminListQuery query,
        CancellationToken cancellationToken) =>
        repositories.Catalog.GetAdminProductsAsync(query, cancellationToken);

    public async Task<ProductAdminDto> GetAsync(Guid productId, CancellationToken cancellationToken) =>
        await repositories.Catalog.GetAdminProductAsync(productId, cancellationToken)
            ?? throw TrackedProducts.NotFound(productId);

    private Task<Product> GetTrackedProductAsync(Guid productId, CancellationToken cancellationToken) =>
        TrackedProducts.GetAsync(repositories, productId, cancellationToken);

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
}
