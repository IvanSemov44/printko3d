namespace Storefront.Domain.Catalog;

public sealed class Category : Entity
{
    private Category()
    {
    }

    public Category(
        string name,
        string slug,
        Category? parent = null,
        string? description = null,
        string? imageKey = null,
        int sortOrder = 0,
        bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        if (parent?.ParentId is not null)
        {
            throw new InvalidOperationException("A category tree has at most two levels.");
        }

        ParentId = parent?.Id;
        Name = name;
        Slug = slug;
        Description = description;
        ImageKey = imageKey;
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    public Guid? ParentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? ImageKey { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }
}
