using Storefront.Application.Catalog.Queries;

namespace Storefront.Application.Catalog;

public interface ICategoryService
{
    /// <summary>Throws ValidationFailedException, NotFoundException (unknown parent) or ConflictException (taken slug).</summary>
    Task<Guid> CreateAsync(NewCategory category, CancellationToken cancellationToken);

    /// <summary>Also throws ConflictException when <see cref="UpdateCategory.Version"/> is stale.</summary>
    Task UpdateAsync(Guid id, UpdateCategory category, CancellationToken cancellationToken);

    /// <summary>Hides or shows the category in the shop. Nothing is deleted and products and subcategories stay.</summary>
    Task SetActiveAsync(Guid id, bool isActive, uint version, CancellationToken cancellationToken);

    /// <summary>Swaps the category with its neighbour in the same group. Does nothing at the edge of the group.</summary>
    Task MoveAsync(Guid id, MoveDirection direction, uint version, CancellationToken cancellationToken);

    /// <summary>All categories, the inactive ones too.</summary>
    Task<IReadOnlyList<CategoryAdminDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<CategoryAdminDto> GetAsync(Guid id, CancellationToken cancellationToken);
}
