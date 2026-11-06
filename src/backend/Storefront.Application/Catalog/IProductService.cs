namespace Storefront.Application.Catalog;

public interface IProductService
{
    Task<Guid> CreateAsync(NewProduct product, CancellationToken cancellationToken);

    Task<Guid> AddVariantAsync(Guid productId, NewVariant variant, CancellationToken cancellationToken);

    Task<Guid> AddImageAsync(Guid productId, NewImage image, CancellationToken cancellationToken);

    Task PublishAsync(Guid productId, CancellationToken cancellationToken);

    Task ArchiveAsync(Guid productId, CancellationToken cancellationToken);
}
