namespace CoffeeShop.Domain.Tests.Catalog;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public class AddonTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));

    private static (MenuItem Item, SizeVariant Variant, Addon Addon) CreateCatalog()
    {
        var category = new Category(EntityId.New(), "Coffee", Now, Now);
        var item = new MenuItem(EntityId.New(), category, "Latte", Now, Now);
        return (item, new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m)),
            new Addon(EntityId.New(), "Extra shot", new Money(10000m)));
    }

    [Fact]
    public void Create_RejectsNameBeyondStorageLimit()
    {
        Assert.Throws<ArgumentException>(() => new Addon(EntityId.New(), new string('a', 81), Money.Zero));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrderLine_RejectsDuplicateAddonSelections(bool separateInstance)
    {
        var (item, variant, addon) = CreateCatalog();
        var duplicate = separateInstance ? new Addon(addon.Id, "Other name", Money.Zero) : addon;

        var exception = Assert.Throws<ArgumentException>(() => new OrderLine(EntityId.New(), item, variant, 1,
            [addon, duplicate], [new MenuItemAddon(item.Id, addon.Id)]));

        Assert.Equal("addons", exception.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(80)]
    public void CreateAndUpdate_NormalizeNameAndPreserveIdentity(int length)
    {
        var id = EntityId.New();
        var addon = new Addon(id, " Extra shot ", Money.Zero);

        Assert.Equal("Extra shot", addon.Name);
        Assert.True(addon.IsActive);
        addon.Update($" {new string('a', length)} ", new Money(10000m));

        Assert.Equal(new string('a', length), addon.Name);
        Assert.Equal(new Money(10000m), addon.Price);
        Assert.Equal(id, addon.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void CreateAndUpdate_RejectInvalidNameWithoutMutation(string? name)
    {
        var (_, _, addon) = CreateCatalog();

        Assert.ThrowsAny<ArgumentException>(() => new Addon(EntityId.New(), name!, Money.Zero));
        Assert.ThrowsAny<ArgumentException>(() => addon.Update(name!, Money.Zero));
        Assert.Equal("Extra shot", addon.Name);
        Assert.Equal(new Money(10000m), addon.Price);
    }

    [Fact]
    public void Update_RejectsOverlongNameAndMissingPriceWithoutMutation()
    {
        var (_, _, addon) = CreateCatalog();

        Assert.Throws<ArgumentException>(() => addon.Update(new string('a', 81), Money.Zero));
        Assert.Throws<ArgumentNullException>(() => addon.Update("Changed", null!));
        Assert.Throws<ArgumentNullException>(() => new Addon(EntityId.New(), "Extra shot", null!));
        Assert.Equal("Extra shot", addon.Name);
        Assert.Equal(new Money(10000m), addon.Price);
    }

    [Fact]
    public void AvailabilityTransitions_PreserveCompatibilityAndSnapshot()
    {
        var (item, _, addon) = CreateCatalog();
        var link = new MenuItemAddon(item.Id, addon.Id);
        var snapshot = new CatalogAddonSnapshot(item, addon, [link]);

        Assert.Throws<InvalidOperationException>(() => addon.Activate());
        addon.Deactivate();
        Assert.False(addon.IsAvailableForOrdering(item, [link]));
        Assert.Throws<InvalidOperationException>(() => addon.Deactivate());
        addon.Activate();

        Assert.True(addon.IsAvailableForOrdering(item, [link]));
        Assert.Equal(item.Id, link.MenuItemId);
        Assert.Equal(addon.Id, link.AddonId);
        Assert.Equal(new Money(10000m), snapshot.UnitPrice);
    }

    [Theory]
    [InlineData("addon")]
    [InlineData("item")]
    [InlineData("missing_link")]
    [InlineData("foreign_item")]
    [InlineData("foreign_addon")]
    public void SnapshotAndOrderLine_RejectUnavailableOrIncompatibleSelection(string reason)
    {
        var (item, variant, addon) = CreateCatalog();
        if (reason == "addon")
        {
            addon.Deactivate();
        }
        if (reason == "item")
        {
            item.Deactivate(Now);
        }
        MenuItemAddon[] links = reason == "missing_link" ? [] :
            [new MenuItemAddon(reason == "foreign_item" ? EntityId.New() : item.Id, reason == "foreign_addon" ? EntityId.New() : addon.Id)];

        Assert.False(addon.IsAvailableForOrdering(item, links));
        Assert.Throws<ArgumentException>(() => new CatalogAddonSnapshot(item, addon, links));
        Assert.Throws<ArgumentException>(() => new OrderLine(EntityId.New(), item, variant, 1, [addon], links));
    }

    [Fact]
    public void OrderLine_RejectsAddonWhenCompatibilityIsNotProvided()
    {
        var (item, variant, addon) = CreateCatalog();

        Assert.Throws<ArgumentException>(() => new OrderLine(EntityId.New(), item, variant, 1, [addon]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void OrderLine_ChargesEachAddonOncePerDrinkAcrossSizesAndPreservesSnapshots(int quantity)
    {
        var (item, large, addon) = CreateCatalog();
        var small = new SizeVariant(EntityId.New(), item.Id, "Small", new Money(30000m));
        var freeAddon = new Addon(EntityId.New(), "Cinnamon", Money.Zero);
        MenuItemAddon[] links = [new(item.Id, addon.Id), new(item.Id, freeAddon.Id)];
        var readContract = new CatalogAddonSnapshot(item, addon, links);
        var smallLine = new OrderLine(EntityId.New(), item, small, quantity, [addon, freeAddon], links);
        var largeLine = new OrderLine(EntityId.New(), item, large, quantity, [addon, freeAddon], links);

        addon.Update("Double shot", new Money(20000m));
        addon.Deactivate();

        Assert.Equal(new Money(40000m * quantity), smallLine.LineTotal);
        Assert.Equal(new Money(55000m * quantity), largeLine.LineTotal);
        Assert.Equal(2, largeLine.Addons.Count);
        Assert.Equal("Extra shot", largeLine.Addons.First().AddonNameSnapshot);
        Assert.Equal(new Money(10000m), largeLine.Addons.First().AddonPriceSnapshot);
        Assert.Equal(addon.Id, largeLine.Addons.First().AddonId);
        Assert.Equal(item.Id, readContract.MenuItemId);
        Assert.Equal(addon.Id, readContract.AddonId);
        Assert.Equal("Extra shot", readContract.Name);
        Assert.Equal(new Money(10000m), readContract.UnitPrice);
        Assert.Equal(1, readContract.MaximumQuantityPerDrink);
        Assert.All(typeof(CatalogAddonSnapshot).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.Throws<ArgumentException>(() => new OrderLine(EntityId.New(), item, large, quantity, [addon], links));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OrderLine_RejectsNonPositiveDrinkQuantity(int quantity)
    {
        var (item, variant, addon) = CreateCatalog();

        Assert.Throws<ArgumentOutOfRangeException>(() => new OrderLine(EntityId.New(), item, variant, quantity,
            [addon], [new MenuItemAddon(item.Id, addon.Id)]));
    }

    [Fact]
    public void OrderLine_NoAddonsRemainsOptional()
    {
        var (item, variant, _) = CreateCatalog();
        var line = new OrderLine(EntityId.New(), item, variant, 2);

        Assert.Empty(line.Addons);
        Assert.Equal(new Money(90000m), line.LineTotal);
    }
}
