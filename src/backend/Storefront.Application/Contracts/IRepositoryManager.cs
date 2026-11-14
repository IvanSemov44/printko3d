namespace Storefront.Application.Contracts;

public interface IRepositoryManager
{
    ICategoryRepository Category { get; }

    IProductRepository Product { get; }

    IStoreSettingsRepository StoreSettings { get; }

    ICartRepository Cart { get; }

    ICartQueries CartQueries { get; }

    IOrderRepository Order { get; }

    IShippingRateRepository ShippingRates { get; }

    IPaymentRepository Payments { get; }

    IOutgoingEmailRepository OutgoingEmails { get; }

    ICatalogQueries Catalog { get; }

    Task SaveAsync(CancellationToken cancellationToken);

    /// <summary>Starts a transaction for writes that must succeed or fail together, such as an order and its stock.</summary>
    Task<IRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}
