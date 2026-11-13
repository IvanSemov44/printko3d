using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

internal sealed class ProductVariantService(IRepositoryManager repositories)
{
    public async Task<Guid> AddVariantAsync(Guid productId, NewVariant input, CancellationToken cancellationToken)
    {
        var product = await TrackedProducts.GetAsync(repositories, productId, cancellationToken);
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
        var product = await TrackedProducts.GetAsync(repositories, productId, cancellationToken);
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
        var product = await TrackedProducts.GetAsync(repositories, productId, cancellationToken);
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
        var product = await TrackedProducts.GetAsync(repositories, productId, cancellationToken);
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

    private static ProductVariant FindVariant(Product product, Guid variantId) =>
        product.Variants.SingleOrDefault(v => v.Id == variantId)
            ?? throw new NotFoundException($"Variant {variantId} was not found.", "variant.not_found");

    private async Task EnsureSkuIsFreeAsync(string sku, Guid? exceptVariantId, CancellationToken cancellationToken)
    {
        if (await repositories.Product.ExistsBySkuAsync(sku, exceptVariantId, cancellationToken))
        {
            throw new ConflictException($"The SKU '{sku}' is already used.", "variant.sku_taken");
        }
    }
}
