using Storefront.Domain.Shipping;

namespace Storefront.Application.Contracts;

public interface IShippingRateRepository
{
    void Create(ShippingRate rate);

    /// <summary>The steps of a method, from the lightest to the heaviest. A method has a handful of steps, so there is no paging.</summary>
    Task<IReadOnlyList<ShippingRate>> ListAsync(string methodCode, CancellationToken cancellationToken);

    Task<bool> AnyAsync(string methodCode, CancellationToken cancellationToken);

    /// <summary>Deletes every step of a method at once. It does not wait for a save, so call it inside a transaction.</summary>
    Task DeleteAllAsync(string methodCode, CancellationToken cancellationToken);
}
