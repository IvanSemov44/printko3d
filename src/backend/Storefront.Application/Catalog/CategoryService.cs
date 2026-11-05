using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

internal sealed class CategoryService(IRepositoryManager repositories) : ICategoryService
{
    public async Task<Guid> CreateAsync(NewCategory input, CancellationToken cancellationToken)
    {
        Guard.NotBlank(input.Name, nameof(input.Name));
        Guard.NotBlank(input.Slug, nameof(input.Slug));

        if (input.ParentId is { } parentId)
        {
            var parent = await repositories.Category.GetByIdAsync(parentId, trackChanges: false, cancellationToken)
                ?? throw new NotFoundException($"Category {parentId} was not found.");
            if (parent.ParentId is not null)
            {
                throw new ValidationFailedException("A category tree has at most two levels.");
            }
        }

        var category = new Category
        {
            ParentId = input.ParentId,
            Name = input.Name,
            Slug = input.Slug,
            Description = input.Description,
            ImageKey = input.ImageKey,
            SortOrder = input.SortOrder,
            IsActive = input.IsActive,
        };
        repositories.Category.Create(category);
        await repositories.SaveAsync(cancellationToken);
        return category.Id;
    }
}
