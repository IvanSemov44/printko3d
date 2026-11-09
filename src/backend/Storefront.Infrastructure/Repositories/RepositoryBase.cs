using Microsoft.EntityFrameworkCore;
using Storefront.Domain;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal abstract class RepositoryBase<T>(StorefrontDbContext dbContext)
    where T : Entity
{
    protected StorefrontDbContext Context { get; } = dbContext;

    public void Create(T entity) => Context.Set<T>().Add(entity);

    protected IQueryable<T> Query(bool trackChanges) =>
        trackChanges ? Context.Set<T>() : Context.Set<T>().AsNoTracking();
}
