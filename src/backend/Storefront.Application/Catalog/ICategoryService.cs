namespace Storefront.Application.Catalog;

public interface ICategoryService
{
    Task<Guid> CreateAsync(NewCategory category, CancellationToken cancellationToken);
}
