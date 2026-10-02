namespace CoffeeShop.Domain.Tests.Catalog;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

public class CatalogLifecycleTests
{
    private static readonly UtcTimestamp Created = new(new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero));
    private static readonly UtcTimestamp Changed = new(Created.Value.AddHours(1));

    private static Category CreateCategory() => new(EntityId.New(), "Coffee", Created, Created);

    [Fact]
    public void Category_CreateRenameAndReactivate_PreserveIdentityAndCreationTime()
    {
        var category = new Category(EntityId.New(), " Coffee ", Created, Created, displayOrder: 3);
        var id = category.Id;

        Assert.Equal("Coffee", category.Name);
        Assert.True(category.IsActive);
        category.Rename(" Iced drinks ", Changed);
        Assert.Equal("Iced drinks", category.Name);
        Assert.Equal(Changed, category.UpdatedAt);

        category.Deactivate(Changed);
        Assert.False(category.IsActive);
        category.Activate(Changed);
        Assert.True(category.IsActive);
        Assert.Equal(id, category.Id);
        Assert.Equal(Created, category.CreatedAt);
        Assert.Equal(3, category.DisplayOrder);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Category_RejectsMissingNamesWithoutMutation(string? name)
    {
        var category = CreateCategory();

        Assert.ThrowsAny<ArgumentException>(() => new Category(EntityId.New(), name!, Created, Created));
        Assert.ThrowsAny<ArgumentException>(() => category.Rename(name!, Changed));
        Assert.Equal("Coffee", category.Name);
        Assert.Equal(Created, category.UpdatedAt);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(80)]
    public void Category_AcceptsTrimmedNameStorageBoundaries(int length)
    {
        var name = new string('a', length);
        var category = new Category(EntityId.New(), $" {name} ", Created, Created);
        category.Rename($" {name} ", Changed);
        Assert.Equal(name, category.Name);
    }

    [Fact]
    public void Category_RejectsOverlongNamesWithoutMutation()
    {
        var category = CreateCategory();
        var name = new string('a', 81);

        Assert.Throws<ArgumentException>(() => new Category(EntityId.New(), name, Created, Created));
        Assert.Throws<ArgumentException>(() => category.Rename(name, Changed));
        Assert.Equal("Coffee", category.Name);
        Assert.Equal(Created, category.UpdatedAt);
    }

    [Fact]
    public void Category_RejectsRepeatedTransitionsWithoutChangingTimestamp()
    {
        var category = CreateCategory();

        Assert.Throws<InvalidOperationException>(() => category.Activate(Changed));
        Assert.Equal(Created, category.UpdatedAt);
        category.Deactivate(Changed);
        Assert.Throws<InvalidOperationException>(() => category.Deactivate(Created));
        Assert.Equal(Changed, category.UpdatedAt);
        Assert.False(category.IsActive);
    }

    [Fact]
    public void MenuItem_UpdateAndReactivate_ValidateCategoryAndPreserveIdentity()
    {
        var category = CreateCategory();
        var item = new MenuItem(EntityId.New(), category, " Latte ", Created, Created, "Milk coffee");
        var id = item.Id;
        var otherCategory = CreateCategory();

        Assert.Equal("Latte", item.Name);
        Assert.Equal(category.Id, item.CategoryId);
        Assert.True(item.IsAvailableForOrdering(category));
        item.Update(" Iced latte ", "Cold milk coffee", otherCategory, Changed);
        Assert.Equal("Iced latte", item.Name);
        Assert.Equal("Cold milk coffee", item.Description);
        Assert.Equal(otherCategory.Id, item.CategoryId);
        Assert.Equal(Changed, item.UpdatedAt);
        Assert.False(item.IsAvailableForOrdering(category));

        item.Deactivate(Changed);
        Assert.False(item.IsAvailableForOrdering(otherCategory));
        item.Activate(otherCategory, Changed);
        Assert.True(item.IsAvailableForOrdering(otherCategory));
        Assert.Equal(id, item.Id);
        Assert.Equal(Created, item.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void MenuItem_RejectsMissingNamesWithoutMutation(string? name)
    {
        var category = CreateCategory();
        var item = new MenuItem(EntityId.New(), category, "Latte", Created, Created, "Original");

        Assert.ThrowsAny<ArgumentException>(() => new MenuItem(EntityId.New(), category, name!, Created, Created));
        Assert.ThrowsAny<ArgumentException>(() => item.Update(name!, "Changed", CreateCategory(), Changed));
        Assert.Equal("Latte", item.Name);
        Assert.Equal("Original", item.Description);
        Assert.Equal(category.Id, item.CategoryId);
        Assert.Equal(Created, item.UpdatedAt);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(120)]
    public void MenuItem_AcceptsTrimmedNameStorageBoundaries(int length)
    {
        var category = CreateCategory();
        var name = new string('a', length);
        var item = new MenuItem(EntityId.New(), category, $" {name} ", Created, Created);
        item.Update($" {name} ", null, category, Changed);
        Assert.Equal(name, item.Name);
        Assert.Null(item.Description);
    }

    [Fact]
    public void MenuItem_RejectsOverlongNamesWithoutMutation()
    {
        var category = CreateCategory();
        var item = new MenuItem(EntityId.New(), category, "Latte", Created, Created);
        var name = new string('a', 121);

        Assert.Throws<ArgumentException>(() => new MenuItem(EntityId.New(), category, name, Created, Created));
        Assert.Throws<ArgumentException>(() => item.Update(name, "Changed", CreateCategory(), Changed));
        Assert.Equal("Latte", item.Name);
        Assert.Equal(category.Id, item.CategoryId);
        Assert.Equal(Created, item.UpdatedAt);
    }

    [Fact]
    public void MenuItem_RejectsMissingOrInactiveCategoryWithoutMutation()
    {
        var category = CreateCategory();
        var item = new MenuItem(EntityId.New(), category, "Latte", Created, Created);
        var inactive = CreateCategory();
        inactive.Deactivate(Changed);

        Assert.Throws<ArgumentNullException>(() => new MenuItem(EntityId.New(), null!, "Latte", Created, Created));
        Assert.Throws<InvalidOperationException>(() => new MenuItem(EntityId.New(), inactive, "Latte", Created, Created));
        Assert.Throws<ArgumentNullException>(() => item.Update("Changed", null, null!, Changed));
        Assert.Throws<InvalidOperationException>(() => item.Update("Changed", null, inactive, Changed));
        Assert.Equal("Latte", item.Name);
        Assert.Equal(category.Id, item.CategoryId);
        Assert.Equal(Created, item.UpdatedAt);
    }

    [Fact]
    public void MenuItem_ActivationRequiresItsOwnActiveCategory()
    {
        var category = CreateCategory();
        var item = new MenuItem(EntityId.New(), category, "Latte", Created, Created);
        item.Deactivate(Changed);
        category.Deactivate(Changed);

        Assert.Throws<InvalidOperationException>(() => item.Activate(category, Created));
        Assert.Throws<ArgumentException>(() => item.Activate(CreateCategory(), Created));
        Assert.Throws<ArgumentNullException>(() => item.Activate(null!, Created));
        Assert.False(item.IsActive);
        Assert.Equal(Changed, item.UpdatedAt);
        category.Activate(Changed);
        item.Activate(category, Changed);
        Assert.True(item.IsActive);
    }

    [Fact]
    public void MenuItem_RejectsRepeatedTransitionsWithoutChangingTimestamp()
    {
        var category = CreateCategory();
        var item = new MenuItem(EntityId.New(), category, "Latte", Created, Created);

        Assert.Throws<InvalidOperationException>(() => item.Activate(category, Changed));
        Assert.Equal(Created, item.UpdatedAt);
        item.Deactivate(Changed);
        Assert.Throws<InvalidOperationException>(() => item.Deactivate(Created));
        Assert.Equal(Changed, item.UpdatedAt);
        Assert.False(item.IsActive);
    }

    [Fact]
    public void Deactivation_PreservesHistoricalReferencesAndDoesNotCascade()
    {
        var category = CreateCategory();
        var item = new MenuItem(EntityId.New(), category, "Latte", Created, Created, "Milk coffee");
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m));
        var compatibility = new MenuItemAddon(item.Id, EntityId.New());
        var historicalItemId = item.Id;
        var historicalCategoryId = item.CategoryId;

        category.Deactivate(Changed);
        Assert.True(item.IsActive);
        Assert.False(item.IsAvailableForOrdering(category));
        item.Deactivate(Changed);

        Assert.Equal(historicalItemId, item.Id);
        Assert.Equal(historicalCategoryId, item.CategoryId);
        Assert.Equal(item.Id, variant.MenuItemId);
        Assert.Equal(item.Id, compatibility.MenuItemId);
        Assert.True(variant.IsActive);
        Assert.Equal(new Money(45000m), variant.Price);
        Assert.Equal("Latte", item.Name);
        Assert.Equal("Milk coffee", item.Description);
        Assert.Equal(Created, item.CreatedAt);
        Assert.Equal(Changed, item.UpdatedAt);
    }
}
