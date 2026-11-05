using Microsoft.EntityFrameworkCore;
using Storefront.Domain;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal abstract class RepositoryBase<T>(StorefrontDbContext context)
    where T : Entity
{
    public void Create(T entity) => context.Set<T>().Add(entity);

    protected IQueryable<T> Query(bool trackChanges) =>
        trackChanges ? context.Set<T>() : context.Set<T>().AsNoTracking();
}
