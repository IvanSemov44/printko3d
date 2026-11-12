using Microsoft.EntityFrameworkCore;
using Storefront.Application.Carts.Queries;
using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Domain.Carts;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class CartQueries(StorefrontDbContext context) : ICartQueries
{
    public async Task<CartDto> GetAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        // One query: the lines of the cart joined to their variant and product, so price and name are always current.
        var rows = await (
            from cart in context.Carts.AsNoTracking()
            where cart.TokenHash == tokenHash
            from item in cart.Items
            join variant in context.ProductVariants on item.VariantId equals variant.Id
            join product in context.Products on variant.ProductId equals product.Id
            orderby item.CreatedAt, item.Id
            select new
            {
                item.VariantId,
                item.Quantity,
                ProductName = product.Name,
                ProductSlug = product.Slug,
                VariantName = variant.Name,
                variant.Sku,
                variant.Price,
                variant.IsActive,
                variant.StockQuantity,
                product.Status,
                product.FulfilmentType,
                ImageKey = product.Images.Where(i => i.IsPrimary).Select(i => i.StorageKey).FirstOrDefault(),
                ImageAlt = product.Images.Where(i => i.IsPrimary).Select(i => i.AltText).FirstOrDefault(),
            })
            .Take(Cart.MaxItems)
            .ToListAsync(cancellationToken);

        var lines = rows.Select(r => new CartLineDto(
            r.VariantId,
            r.ProductName,
            r.ProductSlug,
            r.VariantName,
            r.Sku,
            r.ImageKey is null ? null : new ImageRefDto(r.ImageKey, r.ImageAlt ?? string.Empty),
            r.Price,
            r.Quantity,
            r.Price * r.Quantity,
            CartLineRules.StateOf(r.Status, r.FulfilmentType, r.IsActive, r.StockQuantity, r.Quantity))).ToList();
        return CartDto.From(lines);
    }
}
