namespace CoffeeShop.Domain.Tests.Catalog;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Sales;

public class SizeVariantTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));

    private static (Category Category, MenuItem Item) CreateItem()
    {
        var category = new Category(EntityId.New(), "Coffee", Now, Now);
        return (category, new MenuItem(EntityId.New(), category, "Latte", Now, Now));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(40)]
    public void CreateAndUpdate_NormalizeSizeNameAndPreserveIdentity(int length)
    {
        var id = EntityId.New();
        var itemId = EntityId.New();
        var name = new string('a', length);
        var variant = new SizeVariant(id, itemId, " Small ", Money.Zero);

        Assert.Equal("Small", variant.SizeName);
        Assert.True(variant.IsActive);
        variant.Update($" {name} ", new Money(45000m));

        Assert.Equal(name, variant.SizeName);
        Assert.Equal(new Money(45000m), variant.Price);
        Assert.Equal(id, variant.Id);
        Assert.Equal(itemId, variant.MenuItemId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("12345678901234567890123456789012345678901")]
    public void CreateAndUpdate_RejectInvalidSizeNameWithoutMutation(string? name)
    {
        var variant = new SizeVariant(EntityId.New(), EntityId.New(), "Large", new Money(45000m));

        Assert.ThrowsAny<ArgumentException>(() => new SizeVariant(EntityId.New(), variant.MenuItemId, name!, Money.Zero));
        Assert.ThrowsAny<ArgumentException>(() => variant.Update(name!, Money.Zero));
        Assert.Equal("Large", variant.SizeName);
        Assert.Equal(new Money(45000m), variant.Price);
    }

    [Fact]
    public void CreateAndUpdate_RejectMissingPriceWithoutMutation()
    {
        var variant = new SizeVariant(EntityId.New(), EntityId.New(), "Large", new Money(45000m));

        Assert.Throws<ArgumentNullException>(() => new SizeVariant(EntityId.New(), variant.MenuItemId, "Large", null!));
        Assert.Throws<ArgumentNullException>(() => variant.Update("Small", null!));
        Assert.Equal("Large", variant.SizeName);
        Assert.Equal(new Money(45000m), variant.Price);
    }

    [Fact]
    public void AvailabilityTransitions_PreserveVariantAndRecipeReferences()
    {
        var (_, item) = CreateItem();
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m));
        var ingredient = new Ingredient(EntityId.New(), "Coffee", "g", Now, Now);
        var recipe = new RecipeIngredient(EntityId.New(), variant, ingredient, 18m, "g");

        Assert.True(variant.IsAvailableForOrdering(item));
        Assert.Throws<InvalidOperationException>(() => variant.Activate());
        variant.Deactivate();
        Assert.False(variant.IsAvailableForOrdering(item));
        Assert.Throws<InvalidOperationException>(() => variant.Deactivate());
        variant.Activate();

        Assert.True(variant.IsAvailableForOrdering(item));
        Assert.Equal(variant.Id, recipe.SizeVariantId);
        Assert.Equal(item.Id, variant.MenuItemId);
        Assert.Equal("Large", variant.SizeName);
        Assert.Equal(new Money(45000m), variant.Price);
    }

    [Theory]
    [InlineData("item")]
    [InlineData("variant")]
    [InlineData("foreign")]
    public void SnapshotAndOrderLine_RejectUnavailableOrForeignSelection(string reason)
    {
        var (_, item) = CreateItem();
        var variant = new SizeVariant(EntityId.New(), reason == "foreign" ? EntityId.New() : item.Id, "Large", Money.Zero);
        if (reason == "item")
        {
            item.Deactivate(Now);
        }
        if (reason == "variant")
        {
            variant.Deactivate();
        }

        Assert.False(variant.IsAvailableForOrdering(item));
        Assert.Throws<ArgumentException>(() => new CatalogPriceSnapshot(item, variant));
        Assert.Throws<ArgumentException>(() => new OrderLine(EntityId.New(), item, variant, 1));
    }

    [Fact]
    public void SnapshotAndOrderLine_KeepExactVariantPriceAfterCatalogChanges()
    {
        var (category, item) = CreateItem();
        var small = new SizeVariant(EntityId.New(), item.Id, "Small", new Money(30000m));
        var large = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m));
        var smallSnapshot = new CatalogPriceSnapshot(item, small);
        var largeSnapshot = new CatalogPriceSnapshot(item, large);
        var line = new OrderLine(EntityId.New(), item, large, 2);

        large.Update("Extra large", new Money(50000m));
        large.Deactivate();
        item.Update("Iced latte", null, category, Now);
        item.Deactivate(Now);

        Assert.Equal(new Money(30000m), smallSnapshot.UnitPrice);
        Assert.Equal(new Money(45000m), largeSnapshot.UnitPrice);
        Assert.Equal(item.Id, largeSnapshot.MenuItemId);
        Assert.Equal(large.Id, largeSnapshot.SizeVariantId);
        Assert.Equal("Latte", largeSnapshot.ItemName);
        Assert.Equal("Large", largeSnapshot.SizeName);
        Assert.Equal(largeSnapshot.UnitPrice, line.UnitPriceSnapshot);
        Assert.Equal("Latte", line.ItemNameSnapshot);
        Assert.Equal("Large", line.SizeNameSnapshot);
        Assert.Equal(new Money(90000m), line.LineTotal);
        Assert.All(typeof(CatalogPriceSnapshot).GetProperties(), property => Assert.Null(property.SetMethod));
    }
}
