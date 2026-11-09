using Storefront.Application.Catalog;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

public sealed class CategoryManagementTests
{
    private readonly FakeRepositoryManager _repositories = new();
    private readonly ServiceManager _services;

    public CategoryManagementTests() => _services = new ServiceManager(_repositories);

    private Category Add(string slug, Category? parent = null, int sortOrder = 0, bool isActive = true, uint version = 1)
    {
        var category = new Category
        {
            Name = slug,
            Slug = slug,
            ParentId = parent?.Id,
            SortOrder = sortOrder,
            IsActive = isActive,
            Version = version,
        };
        _repositories.Categories.Add(category);
        return category;
    }

    private static UpdateCategory Edit(
        Category category,
        string? name = null,
        string? slug = null,
        Guid? parentId = null,
        bool keepParent = true,
        uint? version = null) =>
        new(
            name ?? category.Name,
            slug ?? category.Slug,
            keepParent ? category.ParentId : parentId,
            category.Description,
            category.SortOrder,
            category.IsActive,
            version ?? category.Version);

    private static string[] Order(IEnumerable<Category> categories) =>
        categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Select(c => c.Slug).ToArray();

    // --- Create ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithoutName_IsRejectedOnTheNameField(string name)
    {
        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Category.CreateAsync(new NewCategory(name, "figures"), default));

        Assert.Contains("name", error.Errors.Keys);
        Assert.Empty(_repositories.Categories);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Figures")]
    [InlineData("two words")]
    [InlineData("-figures")]
    [InlineData("figures-")]
    [InlineData("fig--ures")]
    [InlineData("фигури")]
    [InlineData("figures\n")]
    public async Task Create_WithABadSlug_IsRejectedOnTheSlugField(string slug)
    {
        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Category.CreateAsync(new NewCategory("Figures", slug), default));

        Assert.Contains("slug", error.Errors.Keys);
        Assert.Empty(_repositories.Categories);
    }

    [Theory]
    [InlineData("figures")]
    [InlineData("3d-prints")]
    [InlineData("a")]
    public async Task Create_WithAGoodSlug_IsAccepted(string slug)
    {
        await _services.Category.CreateAsync(new NewCategory("Figures", slug), default);

        Assert.Equal(slug, Assert.Single(_repositories.Categories).Slug);
    }

    [Fact]
    public async Task Create_WithATakenSlug_IsAConflictWithACode()
    {
        Add("figures");

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => _services.Category.CreateAsync(new NewCategory("Other", "figures"), default));

        Assert.Equal("category.slug_taken", error.Code);
        Assert.Single(_repositories.Categories);
    }

    [Fact]
    public async Task Create_WithAnUnknownParent_IsNotFound()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.Category.CreateAsync(
                new NewCategory("Dragons", "dragons", ParentId: Guid.CreateVersion7()), default));

        Assert.Equal("category.parent_not_found", error.Code);
    }

    [Fact]
    public async Task Create_UnderASubcategory_IsRejected_BecauseTheTreeHasTwoLevels()
    {
        var root = Add("figures");
        var child = Add("dragons", root);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Category.CreateAsync(new NewCategory("Baby", "baby", ParentId: child.Id), default));

        Assert.Contains("parentId", error.Errors.Keys);
    }

    // --- Update ---

    [Fact]
    public async Task Update_ChangesTheEditableFields_AndKeepsTheImage()
    {
        var category = Add("figures");
        category.ImageKey = "categories/figures.jpg";

        await _services.Category.UpdateAsync(
            category.Id,
            new UpdateCategory("Фигури", "figurki", null, "Описание", 3, false, category.Version),
            default);

        Assert.Equal("Фигури", category.Name);
        Assert.Equal("figurki", category.Slug);
        Assert.Equal("Описание", category.Description);
        Assert.Equal(3, category.SortOrder);
        Assert.False(category.IsActive);
        Assert.Equal("categories/figures.jpg", category.ImageKey);
        Assert.Equal(1, _repositories.SaveCount);
    }

    [Fact]
    public async Task Update_KeepingTheOwnSlug_IsAccepted()
    {
        var category = Add("figures");

        await _services.Category.UpdateAsync(category.Id, Edit(category, name: "New name"), default);

        Assert.Equal("New name", category.Name);
    }

    [Fact]
    public async Task Update_ToTheSlugOfAnotherCategory_IsAConflictWithACode()
    {
        Add("figures");
        var other = Add("toys");

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => _services.Category.UpdateAsync(other.Id, Edit(other, slug: "figures"), default));

        Assert.Equal("category.slug_taken", error.Code);
        Assert.Equal("toys", other.Slug);
    }

    [Fact]
    public async Task Update_OfAnUnknownCategory_IsNotFound()
    {
        var category = new Category { Name = "x", Slug = "x" };

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.Category.UpdateAsync(Guid.CreateVersion7(), Edit(category), default));

        Assert.Equal("category.not_found", error.Code);
    }

    [Fact]
    public async Task Update_WithAStaleVersion_IsAConflict_AndNothingIsSaved()
    {
        var category = Add("figures", version: 5);

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => _services.Category.UpdateAsync(category.Id, Edit(category, name: "Late", version: 4), default));

        Assert.Equal("category.version_conflict", error.Code);
        Assert.Equal(0, _repositories.SaveCount);
    }

    [Fact]
    public async Task Update_CategoryAsItsOwnParent_IsRejected()
    {
        var category = Add("figures");

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Category.UpdateAsync(
                category.Id, Edit(category, parentId: category.Id, keepParent: false), default));

        Assert.Contains("parentId", error.Errors.Keys);
        Assert.Null(category.ParentId);
    }

    [Fact]
    public async Task Update_ParentThatIsASubcategory_IsRejected()
    {
        var root = Add("figures");
        var child = Add("dragons", root);
        var other = Add("toys");

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Category.UpdateAsync(
                other.Id, Edit(other, parentId: child.Id, keepParent: false), default));
    }

    [Fact]
    public async Task Update_CategoryWithSubcategories_CannotBecomeASubcategory()
    {
        var root = Add("figures");
        Add("dragons", root);
        var other = Add("toys");

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Category.UpdateAsync(
                root.Id, Edit(root, parentId: other.Id, keepParent: false), default));

        Assert.Contains("parentId", error.Errors.Keys);
        Assert.Null(root.ParentId);
    }

    [Fact]
    public async Task Update_WithAnUnknownParent_IsNotFound()
    {
        var category = Add("figures");

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.Category.UpdateAsync(
                category.Id, Edit(category, parentId: Guid.CreateVersion7(), keepParent: false), default));

        Assert.Equal("category.parent_not_found", error.Code);
    }

    [Fact]
    public async Task Update_CanMoveACategoryUnderARootAndBackToTheTop()
    {
        var root = Add("figures");
        var other = Add("toys");

        await _services.Category.UpdateAsync(other.Id, Edit(other, parentId: root.Id, keepParent: false), default);
        Assert.Equal(root.Id, other.ParentId);

        await _services.Category.UpdateAsync(other.Id, Edit(other, parentId: null, keepParent: false), default);
        Assert.Null(other.ParentId);
    }

    [Fact]
    public async Task Update_WithABadSlug_IsRejected()
    {
        var category = Add("figures");

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.Category.UpdateAsync(category.Id, Edit(category, slug: "Bad Slug"), default));

        Assert.Contains("slug", error.Errors.Keys);
    }

    // --- Activate and deactivate ---

    [Fact]
    public async Task Deactivate_HidesOnlyThatCategory_AndKeepsItsChildren()
    {
        var root = Add("figures");
        var child = Add("dragons", root);

        await _services.Category.SetActiveAsync(root.Id, false, root.Version, default);

        Assert.False(root.IsActive);
        Assert.True(child.IsActive);
        Assert.Equal(2, _repositories.Categories.Count);
    }

    [Fact]
    public async Task Activate_ShowsTheCategoryAgain()
    {
        var category = Add("figures", isActive: false);

        await _services.Category.SetActiveAsync(category.Id, true, category.Version, default);

        Assert.True(category.IsActive);
    }

    [Fact]
    public async Task Deactivate_WithAStaleVersion_IsAConflict()
    {
        var category = Add("figures", version: 5);

        await Assert.ThrowsAsync<ConflictException>(
            () => _services.Category.SetActiveAsync(category.Id, false, 4, default));

        Assert.Equal(0, _repositories.SaveCount);
    }

    [Fact]
    public async Task Deactivate_OfAnUnknownCategory_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _services.Category.SetActiveAsync(Guid.CreateVersion7(), false, 1, default));
    }

    // --- Move ---

    [Fact]
    public async Task MoveUp_SwapsWithThePreviousNeighbour()
    {
        Add("a", sortOrder: 0);
        var b = Add("b", sortOrder: 1);
        Add("c", sortOrder: 2);

        await _services.Category.MoveAsync(b.Id, MoveDirection.Up, b.Version, default);

        Assert.Equal(["b", "a", "c"], Order(_repositories.Categories));
    }

    [Fact]
    public async Task MoveDown_SwapsWithTheNextNeighbour()
    {
        var a = Add("a", sortOrder: 0);
        Add("b", sortOrder: 1);
        Add("c", sortOrder: 2);

        await _services.Category.MoveAsync(a.Id, MoveDirection.Down, a.Version, default);

        Assert.Equal(["b", "a", "c"], Order(_repositories.Categories));
    }

    [Fact]
    public async Task Move_PastTheEdge_ChangesNothing()
    {
        var a = Add("a", sortOrder: 0);
        var b = Add("b", sortOrder: 1);

        await _services.Category.MoveAsync(a.Id, MoveDirection.Up, a.Version, default);
        await _services.Category.MoveAsync(b.Id, MoveDirection.Down, b.Version, default);

        Assert.Equal(["a", "b"], Order(_repositories.Categories));
    }

    [Fact]
    public async Task Move_WhenSortOrdersAreEqual_StillChangesTheOrder_AndNumbersTheGroup()
    {
        // Equal values fall back to the name, so "a" is first. Moving "b" up must really put it first.
        Add("a");
        var b = Add("b");
        var c = Add("c");

        await _services.Category.MoveAsync(b.Id, MoveDirection.Up, b.Version, default);

        Assert.Equal(["b", "a", "c"], Order(_repositories.Categories));
        Assert.Equal([0, 1, 2], _repositories.Categories.OrderBy(x => x.SortOrder).Select(x => x.SortOrder));
        Assert.Equal(2, c.SortOrder);
    }

    [Fact]
    public async Task Move_StaysInsideTheGroupOfTheSameParent()
    {
        var root = Add("root");
        var first = Add("child-1", root, sortOrder: 0);
        var second = Add("child-2", root, sortOrder: 1);
        var topLevel = Add("other-root", sortOrder: 5);

        await _services.Category.MoveAsync(second.Id, MoveDirection.Up, second.Version, default);

        Assert.Equal(0, second.SortOrder);
        Assert.Equal(1, first.SortOrder);
        Assert.Equal(5, topLevel.SortOrder);
    }

    [Fact]
    public async Task Move_WithAStaleVersion_IsAConflict_AndNothingChanges()
    {
        Add("a", sortOrder: 0);
        var b = Add("b", sortOrder: 1, version: 5);

        await Assert.ThrowsAsync<ConflictException>(
            () => _services.Category.MoveAsync(b.Id, MoveDirection.Up, 4, default));

        Assert.Equal(0, _repositories.SaveCount);
    }
}
