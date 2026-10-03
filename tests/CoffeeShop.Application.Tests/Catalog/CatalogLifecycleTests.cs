namespace CoffeeShop.Application.Tests.Catalog;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

public class CatalogLifecycleTests
{
    private static readonly UtcTimestamp Created = new(new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero));
    private static readonly UtcTimestamp Changed = new(Created.Value.AddHours(1));

    private static Category CreateCategory() => new(EntityId.New(), "Coffee", Created, Created);

    [Fact]
    public void ValidOperations_ReturnSuccessAndApplyDomainChanges()
    {
        var categoryResult = CatalogLifecycle.CreateCategory(EntityId.New(), " Coffee ", Created, displayOrder: 2);
        Assert.True(categoryResult.IsSuccess);
        var category = categoryResult.Value;
        Assert.Equal("Coffee", category.Name);
        Assert.Equal(2, category.DisplayOrder);
        Assert.True(CatalogLifecycle.RenameCategory(category, " Iced coffee ", Changed).IsSuccess);
        Assert.Equal("Iced coffee", category.Name);

        var itemResult = CatalogLifecycle.CreateMenuItem(EntityId.New(), category, " Latte ", Created, "Milk coffee");
        Assert.True(itemResult.IsSuccess);
        var item = itemResult.Value;
        Assert.Equal(category.Id, item.CategoryId);
        Assert.Equal(Created, item.CreatedAt);
        Assert.True(CatalogLifecycle.UpdateMenuItem(item, "Iced latte", null, category, Changed).IsSuccess);
        Assert.Equal("Iced latte", item.Name);
        Assert.Null(item.Description);
        Assert.True(CatalogLifecycle.DeactivateMenuItem(item, Changed).IsSuccess);
        Assert.False(item.IsAvailableForOrdering(category));
        Assert.True(CatalogLifecycle.ActivateMenuItem(item, category, Changed).IsSuccess);
        Assert.True(item.IsAvailableForOrdering(category));
        Assert.True(CatalogLifecycle.DeactivateCategory(category, Changed).IsSuccess);
        Assert.False(item.IsAvailableForOrdering(category));
        Assert.True(CatalogLifecycle.ActivateCategory(category, Changed).IsSuccess);
        Assert.True(item.IsAvailableForOrdering(category));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("overlong")]
    public void InvalidNames_ReturnSharedFieldValidationErrorsWithoutMutation(string? value)
    {
        var category = CreateCategory();
        var item = new MenuItem(EntityId.New(), category, "Latte", Created, Created);
        var categoryName = value == "overlong" ? new string('a', 81) : value!;
        var itemName = value == "overlong" ? new string('a', 121) : value!;

        AssertValidation(CatalogLifecycle.CreateCategory(EntityId.New(), categoryName, Created), "name");
        AssertValidation(CatalogLifecycle.RenameCategory(category, categoryName, Changed), "name");
        AssertValidation(CatalogLifecycle.CreateMenuItem(EntityId.New(), category, itemName, Created), "name");
        AssertValidation(CatalogLifecycle.UpdateMenuItem(item, itemName, "Changed", CreateCategory(), Changed), "name");
        Assert.Equal("Coffee", category.Name);
        Assert.Equal("Latte", item.Name);
        Assert.Equal(category.Id, item.CategoryId);
        Assert.Equal(Created, category.UpdatedAt);
        Assert.Equal(Created, item.UpdatedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RepeatedTransitions_ReturnSharedConflictsWithoutMutation(bool active)
    {
        var category = new Category(EntityId.New(), "Coffee", Created, Created, isActive: active);
        var itemCategory = CreateCategory();
        var item = new MenuItem(EntityId.New(), itemCategory, "Latte", Created, Created, isActive: active);

        AssertConflict(active
            ? CatalogLifecycle.ActivateCategory(category, Changed)
            : CatalogLifecycle.DeactivateCategory(category, Changed));
        AssertConflict(active
            ? CatalogLifecycle.ActivateMenuItem(item, itemCategory, Changed)
            : CatalogLifecycle.DeactivateMenuItem(item, Changed));
        Assert.Equal(active, category.IsActive);
        Assert.Equal(active, item.IsActive);
        Assert.Equal(Created, category.UpdatedAt);
        Assert.Equal(Created, item.UpdatedAt);
    }

    [Fact]
    public void MissingCategory_ReturnsSharedValidationErrorWithoutMutation()
    {
        var category = CreateCategory();
        var item = new MenuItem(EntityId.New(), category, "Latte", Created, Created);

        AssertValidation(CatalogLifecycle.CreateMenuItem(EntityId.New(), null!, "Latte", Created), "category");
        AssertValidation(CatalogLifecycle.UpdateMenuItem(item, "Changed", null, null!, Changed), "category");
        item.Deactivate(Changed);
        AssertValidation(CatalogLifecycle.ActivateMenuItem(item, null!, Created), "category");
        Assert.Equal("Latte", item.Name);
        Assert.Equal(category.Id, item.CategoryId);
        Assert.False(item.IsActive);
        Assert.Equal(Changed, item.UpdatedAt);
    }

    [Fact]
    public void InactiveCategory_ReturnsSharedConflictWithoutMutation()
    {
        var category = CreateCategory();
        var item = new MenuItem(EntityId.New(), category, "Latte", Created, Created);
        category.Deactivate(Changed);

        AssertConflict(CatalogLifecycle.CreateMenuItem(EntityId.New(), category, "New drink", Created));
        AssertConflict(CatalogLifecycle.UpdateMenuItem(item, "Changed", null, category, Changed));
        Assert.Equal("Latte", item.Name);
        Assert.Equal(Created, item.UpdatedAt);
        item.Deactivate(Changed);
        AssertConflict(CatalogLifecycle.ActivateMenuItem(item, category, Created));
        Assert.False(item.IsActive);
        Assert.Equal(Changed, item.UpdatedAt);
    }

    [Fact]
    public void WrongCategoryOnActivation_ReturnsSharedValidationError()
    {
        var category = CreateCategory();
        var item = new MenuItem(EntityId.New(), category, "Latte", Created, Created, isActive: false);

        AssertValidation(CatalogLifecycle.ActivateMenuItem(item, CreateCategory(), Changed), "category");
        Assert.False(item.IsActive);
        Assert.Equal(category.Id, item.CategoryId);
        Assert.Equal(Created, item.UpdatedAt);
    }

    private static void AssertValidation(Result result, string target)
    {
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("catalog.invalid_input", result.Error.Code);
        Assert.Equal(target, Assert.Single(result.Error.Details).Target);
    }

    private static void AssertConflict(Result result)
    {
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("catalog.invalid_state", result.Error.Code);
    }
}
