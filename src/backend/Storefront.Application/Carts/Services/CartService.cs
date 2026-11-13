using Storefront.Application.Carts.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Carts;

namespace Storefront.Application.Carts;

internal sealed class CartService(IRepositoryManager repositories) : ICartService
{
    public Task<CartDto> GetAsync(string? token, CancellationToken cancellationToken) =>
        CartToken.TryHash(token, out var hash)
            ? repositories.CartQueries.GetAsync(hash, cancellationToken)
            : Task.FromResult(CartDto.Empty);

    public async Task<CartChange> AddItemAsync(
        string? token, Guid variantId, int quantity, CancellationToken cancellationToken)
    {
        RequirePositive(quantity);
        TooMany(quantity);

        // The same answer for a variant that does not exist and one that cannot be ordered: nothing is revealed.
        var supply = await repositories.Cart.GetSupplyAsync(variantId, cancellationToken);
        if (supply is not { IsOrderable: true })
        {
            throw new NotFoundException("The variant does not exist.", "variant.not_found");
        }

        var (cart, hash, currentToken) = await FindAsync(token, cancellationToken);
        var existing = cart?.Items.SingleOrDefault(i => i.VariantId == variantId);
        // Checked before a cart is created, so a refused add leaves nothing behind.
        RequireInStock(supply, (existing?.Quantity ?? 0) + quantity);

        var isNew = cart is null;
        if (cart is null)
        {
            currentToken = CartToken.Create();
            CartToken.TryHash(currentToken, out hash);
            cart = new Cart { TokenHash = hash };
            repositories.Cart.Create(cart);
        }

        var line = existing;
        if (line is null)
        {
            if (cart.Items.Count >= Cart.MaxItems)
            {
                throw new RuleViolationException(
                    $"A cart holds at most {Cart.MaxItems} different items.", "cart.too_many_items");
            }

            var item = new CartItem { CartId = cart.Id, VariantId = variantId, Quantity = quantity };
            repositories.Cart.AddItem(item);
            cart.Items.Add(item);
        }
        else
        {
            TooMany(line.Quantity + quantity);
            line.Quantity += quantity;
        }

        return await SaveAsync(cart, hash, currentToken, touch: !isNew, cancellationToken);
    }

    public async Task<CartChange> SetQuantityAsync(
        string? token, Guid variantId, int quantity, CancellationToken cancellationToken)
    {
        RequirePositive(quantity);
        TooMany(quantity);

        var (cart, hash, currentToken) = await FindAsync(token, cancellationToken);
        var line = cart?.Items.SingleOrDefault(i => i.VariantId == variantId) ?? throw LineNotFound();
        // Fewer units are always allowed, also when the stock has since fallen below the line.
        if (quantity > line.Quantity)
        {
            RequireInStock(await repositories.Cart.GetSupplyAsync(variantId, cancellationToken), quantity);
        }

        line.Quantity = quantity;
        return await SaveAsync(cart!, hash, currentToken, touch: true, cancellationToken);
    }

    public async Task<CartChange> RemoveItemAsync(string? token, Guid variantId, CancellationToken cancellationToken)
    {
        var (cart, hash, currentToken) = await FindAsync(token, cancellationToken);
        var line = cart?.Items.SingleOrDefault(i => i.VariantId == variantId) ?? throw LineNotFound();
        repositories.Cart.RemoveItem(line);
        return await SaveAsync(cart!, hash, currentToken, touch: true, cancellationToken);
    }

    public async Task<CartChange> ClearAsync(string? token, CancellationToken cancellationToken)
    {
        var (cart, hash, currentToken) = await FindAsync(token, cancellationToken);
        if (cart is null)
        {
            return new CartChange(CartDto.Empty, null);
        }

        repositories.Cart.RemoveItems(cart);
        return await SaveAsync(cart, hash, currentToken, touch: true, cancellationToken);
    }

    private async Task<(Cart? Cart, byte[] Hash, string? Token)> FindAsync(
        string? token, CancellationToken cancellationToken)
    {
        if (!CartToken.TryHash(token, out var hash))
        {
            return (null, [], null);
        }

        var cart = await repositories.Cart.GetByTokenHashAsync(hash, trackChanges: true, cancellationToken);
        return (cart, hash, cart is null ? null : token);
    }

    private async Task<CartChange> SaveAsync(
        Cart cart, byte[] hash, string? token, bool touch, CancellationToken cancellationToken)
    {
        if (touch)
        {
            repositories.Cart.Touch(cart);
        }

        await repositories.SaveAsync(cancellationToken);
        return new CartChange(await repositories.CartQueries.GetAsync(hash, cancellationToken), token);
    }

    private static void RequirePositive(int quantity)
    {
        if (quantity < 1)
        {
            throw new ValidationFailedException(
                "The quantity must be at least 1.",
                new Dictionary<string, string[]> { ["quantity"] = ["The quantity must be at least 1."] });
        }
    }

    private static void TooMany(int quantity)
    {
        if (quantity > Cart.MaxQuantityPerItem)
        {
            throw new RuleViolationException(
                $"At most {Cart.MaxQuantityPerItem} units of one item are allowed.", "cart.quantity_too_high");
        }
    }

    private static void RequireInStock(VariantSupply? supply, int quantity)
    {
        if (supply?.Limit is { } limit && quantity > limit)
        {
            throw new RuleViolationException(
                $"Only {limit} units are in stock.", "cart.quantity_exceeds_stock");
        }
    }

    private static NotFoundException LineNotFound() =>
        new("The item is not in the cart.", "cart.item_not_found");
}
