using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

internal sealed class ProductService(IRepositoryManager repositories) : IProductService
{
    public async Task<Guid> CreateAsync(NewProduct input, CancellationToken cancellationToken)
    {
        Guard.NotBlank(input.Name, nameof(input.Name));
        Guard.NotBlank(input.Slug, nameof(input.Slug));
        Guard.NotNegative(input.LeadTimeDays, nameof(input.LeadTimeDays));
        if (input.ShortDescription.Length > Product.ShortDescriptionMaxLength)
        {
            throw new ValidationFailedException(
                $"The short description is at most {Product.ShortDescriptionMaxLength} characters.");
        }

        if (input.FulfilmentType == FulfilmentType.MadeToOrder && input.LeadTimeDays is null)
        {
            throw new ValidationFailedException("Lead time is required for made-to-order products.");
        }

        _ = await repositories.Category.GetByIdAsync(input.CategoryId, trackChanges: false, cancellationToken)
            ?? throw new NotFoundException($"Category {input.CategoryId} was not found.");

        var product = new Product
        {
            CategoryId = input.CategoryId,
            Name = input.Name,
            Slug = input.Slug,
            ShortDescription = input.ShortDescription,
            Description = input.Description,
            FulfilmentType = input.FulfilmentType,
            LeadTimeDays = input.LeadTimeDays,
            SeoTitle = input.SeoTitle,
            SeoDescription = input.SeoDescription,
        };
        repositories.Product.Create(product);
        await repositories.SaveAsync(cancellationToken);
        return product.Id;
    }

    public async Task<Guid> AddVariantAsync(Guid productId, NewVariant input, CancellationToken cancellationToken)
    {
        Guard.NotBlank(input.Sku, nameof(input.Sku));
        Guard.NotBlank(input.Name, nameof(input.Name));
        Guard.NotNegative(input.Price, nameof(input.Price));
        Guard.NotNegative(input.WeightGrams, nameof(input.WeightGrams));
        Guard.NotNegative(input.CompareAtPrice, nameof(input.CompareAtPrice));
        Guard.NotNegative(input.StockQuantity, nameof(input.StockQuantity));

        var product = await GetTrackedProductAsync(productId, cancellationToken);
        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Sku = input.Sku,
            Name = input.Name,
            Price = input.Price,
            CompareAtPrice = input.CompareAtPrice,
            StockQuantity = input.StockQuantity,
            WeightGrams = input.WeightGrams,
            Attributes = input.Attributes is null ? [] : new Dictionary<string, string>(input.Attributes),
            IsActive = input.IsActive,
            SortOrder = input.SortOrder,
        };
        product.Variants.Add(variant);
        await repositories.SaveAsync(cancellationToken);
        return variant.Id;
    }

    public async Task PublishAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await GetTrackedProductAsync(productId, cancellationToken);
        if (product.Variants.Count == 0)
        {
            throw new ValidationFailedException("A product needs at least one variant to be published.");
        }

        product.Status = ProductStatus.Published;
        await repositories.SaveAsync(cancellationToken);
    }

    private async Task<Product> GetTrackedProductAsync(Guid productId, CancellationToken cancellationToken) =>
        await repositories.Product.GetByIdAsync(productId, trackChanges: true, cancellationToken)
            ?? throw new NotFoundException($"Product {productId} was not found.");
}
