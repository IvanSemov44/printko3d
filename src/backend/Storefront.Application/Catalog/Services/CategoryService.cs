using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

internal sealed class CategoryService(IRepositoryManager repositories) : ICategoryService
{
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 2000;

    public async Task<Guid> CreateAsync(NewCategory input, CancellationToken cancellationToken)
    {
        ValidateFields(input.Name, input.Slug, input.Description, input.SortOrder);
        await EnsureSlugIsFreeAsync(input.Slug, exceptId: null, cancellationToken);
        await EnsureParentAllowsChildAsync(input.ParentId, cancellationToken);

        var category = new Category
        {
            ParentId = input.ParentId,
            Name = input.Name.Trim(),
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

    public async Task UpdateAsync(Guid id, UpdateCategory input, CancellationToken cancellationToken)
    {
        ValidateFields(input.Name, input.Slug, input.Description, input.SortOrder);
        var category = await FindAsync(id, cancellationToken);
        repositories.Category.ExpectVersion(category, input.Version);

        if (input.Slug != category.Slug)
        {
            await EnsureSlugIsFreeAsync(input.Slug, id, cancellationToken);
        }

        if (input.ParentId != category.ParentId)
        {
            await EnsureCanMoveUnderAsync(category, input.ParentId, cancellationToken);
        }

        category.ParentId = input.ParentId;
        category.Name = input.Name.Trim();
        category.Slug = input.Slug;
        category.Description = input.Description;
        category.SortOrder = input.SortOrder;
        category.IsActive = input.IsActive;
        await repositories.SaveAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, uint version, CancellationToken cancellationToken)
    {
        var category = await FindAsync(id, cancellationToken);
        repositories.Category.ExpectVersion(category, version);
        category.IsActive = isActive;
        await repositories.SaveAsync(cancellationToken);
    }

    public async Task MoveAsync(Guid id, MoveDirection direction, uint version, CancellationToken cancellationToken)
    {
        var category = await FindAsync(id, cancellationToken);
        repositories.Category.ExpectVersion(category, version);

        var siblings = (await repositories.Category.GetSiblingsAsync(category.ParentId, cancellationToken)).ToList();
        var index = siblings.FindIndex(s => s.Id == id);
        var target = direction == MoveDirection.Up ? index - 1 : index + 1;
        if (target >= 0 && target < siblings.Count)
        {
            (siblings[index], siblings[target]) = (siblings[target], siblings[index]);
        }

        // Rewrite the whole group as 0, 1, 2...: swapping two equal sortOrders would otherwise change nothing.
        // Rows whose number stays the same are not touched, so they are not written.
        for (var position = 0; position < siblings.Count; position++)
        {
            if (siblings[position].SortOrder != position)
            {
                siblings[position].SortOrder = position;
            }
        }

        await repositories.SaveAsync(cancellationToken);
    }

    public Task<IReadOnlyList<CategoryAdminDto>> GetAllAsync(CancellationToken cancellationToken) =>
        repositories.Catalog.GetAdminCategoriesAsync(cancellationToken);

    public async Task<CategoryAdminDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repositories.Catalog.GetAdminCategoryAsync(id, cancellationToken)
            ?? throw CategoryNotFound(id);

    private static NotFoundException CategoryNotFound(Guid id) =>
        new($"Category {id} was not found.", "category.not_found");

    private async Task<Category> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await repositories.Category.GetByIdAsync(id, trackChanges: true, cancellationToken)
            ?? throw CategoryNotFound(id);

    private static void ValidateFields(string? name, string? slug, string? description, int sortOrder)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (name.Trim().Length > NameMaxLength)
        {
            errors["name"] = [$"Name must be at most {NameMaxLength} characters."];
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            errors["slug"] = ["Slug is required."];
        }
        else if (!SlugRules.IsValid(slug))
        {
            errors["slug"] = [SlugRules.FormatMessage];
        }

        if (description is { Length: > DescriptionMaxLength })
        {
            errors["description"] = [$"Description must be at most {DescriptionMaxLength} characters."];
        }

        if (sortOrder < 0)
        {
            errors["sortOrder"] = ["Sort order must not be negative."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException("The category is not valid.", errors);
        }
    }

    private async Task EnsureSlugIsFreeAsync(string slug, Guid? exceptId, CancellationToken cancellationToken)
    {
        var existing = await repositories.Category.GetBySlugAsync(slug, trackChanges: false, cancellationToken);
        if (existing is not null && existing.Id != exceptId)
        {
            throw new ConflictException($"The slug '{slug}' is already used.", "category.slug_taken");
        }
    }

    private async Task EnsureParentAllowsChildAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId is not { } id)
        {
            return;
        }

        var parent = await repositories.Category.GetByIdAsync(id, trackChanges: false, cancellationToken)
            ?? throw new NotFoundException($"Category {id} was not found.", "category.parent_not_found");
        if (parent.ParentId is not null)
        {
            throw new ValidationFailedException(
                "A category tree has at most two levels.",
                new Dictionary<string, string[]> { ["parentId"] = ["The parent is already a subcategory."] });
        }
    }

    private async Task EnsureCanMoveUnderAsync(Category category, Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId == category.Id)
        {
            throw new ValidationFailedException(
                "A category cannot be its own parent.",
                new Dictionary<string, string[]> { ["parentId"] = ["A category cannot be its own parent."] });
        }

        if (parentId is not null && await repositories.Category.HasChildrenAsync(category.Id, cancellationToken))
        {
            throw new ValidationFailedException(
                "A category with subcategories cannot become a subcategory.",
                new Dictionary<string, string[]> { ["parentId"] = ["This category has subcategories."] });
        }

        await EnsureParentAllowsChildAsync(parentId, cancellationToken);
    }
}
