using Storefront.Domain.Shipping;

namespace Storefront.Application.Contracts;

public interface IShippingRateRepository
{
    void Create(ShippingRate rate);

    /// <summary>The steps of a method, from the lightest to the heaviest. A method has a handful of steps, so there is no paging.</summary>
    Task<IReadOnlyList<ShippingRate>> ListAsync(string methodCode, CancellationToken cancellationToken);

    Task<bool> AnyAsync(string methodCode, CancellationToken cancellationToken);
}
