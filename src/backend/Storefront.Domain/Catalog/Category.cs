namespace Storefront.Domain.Catalog;

public sealed class Category : Entity
{
    public Guid? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageKey { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
