namespace CoffeeShop.Application.Tests.Catalog;

using System.Globalization;
using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

public class CatalogAddonsTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));

    private static (Category Category, MenuItem Item) CreateItem()
    {
        var category = new Category(EntityId.New(), "Coffee", Now, Now);
        return (category, new MenuItem(EntityId.New(), category, "Latte", Now, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("10000000000")]
    public void CreateAndUpdate_RejectInvalidPriceWithoutMutation(string? value)
    {
        var addon = new Addon(EntityId.New(), "Extra shot", new Money(10000m));
        var price = value is null ? (decimal?)null : decimal.Parse(value, CultureInfo.InvariantCulture);

        var created = CatalogAddons.CreateAddon(EntityId.New(), "Extra shot", price);
        var updated = CatalogAddons.UpdateAddon(addon, "Changed", price);

        Assert.Equal(ErrorType.Validation, created.Error!.Type);
        Assert.Equal(ErrorType.Validation, updated.Error!.Type);
        Assert.Single(created.Error.Details);
        Assert.Equal("Extra shot", addon.Name);
        Assert.Equal(new Money(10000m), addon.Price);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.01")]
    [InlineData("9999999999.99")]
    public void CreateAndUpdate_AcceptPriceStorageBoundaries(string value)
    {
        var price = decimal.Parse(value, CultureInfo.InvariantCulture);
        var addon = CatalogAddons.CreateAddon(EntityId.New(), " Extra shot ", price).Value;

        Assert.Equal("Extra shot", addon.Name);
        Assert.Equal(new Money(price), addon.Price);
        Assert.True(CatalogAddons.UpdateAddon(addon, " Cinnamon ", price).IsSuccess);
        Assert.Equal("Cinnamon", addon.Name);
        Assert.Equal(new Money(price), addon.Price);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("overlong")]
    public void CreateAndUpdate_RejectInvalidNamesWithoutMutation(string? name)
    {
        name = name == "overlong" ? new string('a', 81) : name;
        var addon = new Addon(EntityId.New(), "Extra shot", new Money(10000m));

        Assert.Equal(ErrorType.Validation, CatalogAddons.CreateAddon(EntityId.New(), name!, 0m).Error!.Type);
        Assert.Equal(ErrorType.Validation, CatalogAddons.UpdateAddon(addon, name!, 0m).Error!.Type);
        Assert.Equal("Extra shot", addon.Name);
        Assert.Equal(new Money(10000m), addon.Price);
    }

    [Fact]
    public void AvailabilityTransitions_MapRepeatedStatesToConflict()
    {
        var addon = CatalogAddons.CreateAddon(EntityId.New(), "Extra shot", 10000m).Value;

        Assert.Equal(ErrorType.Conflict, CatalogAddons.ActivateAddon(addon).Error!.Type);
        Assert.True(CatalogAddons.DeactivateAddon(addon).IsSuccess);
        Assert.False(addon.IsActive);
        Assert.Equal(ErrorType.Conflict, CatalogAddons.DeactivateAddon(addon).Error!.Type);
        Assert.True(CatalogAddons.ActivateAddon(addon).IsSuccess);
        Assert.True(addon.IsActive);
    }

    [Fact]
    public void GetAllowedAddons_ReturnsOnlyActiveCompatibleDefinitionsAsImmutableReadContracts()
    {
        var (category, item) = CreateItem();
        var allowed = new Addon(EntityId.New(), "Extra shot", new Money(10000m));
        var inactive = new Addon(EntityId.New(), "Syrup", Money.Zero, isActive: false);
        var incompatible = new Addon(EntityId.New(), "Cream", Money.Zero);
        MenuItemAddon[] links = [new(item.Id, allowed.Id), new(item.Id, inactive.Id), new(EntityId.New(), incompatible.Id)];

        var snapshot = Assert.Single(CatalogAddons.GetAllowedAddons(item, category, [allowed, inactive, incompatible], links).Value);
        allowed.Update("Changed", Money.Zero);
        allowed.Deactivate();

        Assert.Equal(item.Id, snapshot.MenuItemId);
        Assert.Equal(allowed.Id, snapshot.AddonId);
        Assert.Equal("Extra shot", snapshot.Name);
        Assert.Equal(new Money(10000m), snapshot.UnitPrice);
        Assert.Equal(1, snapshot.MaximumQuantityPerDrink);
        Assert.Empty(CatalogAddons.GetAllowedAddons(item, category, [allowed, inactive, incompatible], links).Value);
        Assert.True(CatalogAddons.ActivateAddon(allowed).IsSuccess);
        Assert.Single(CatalogAddons.GetAllowedAddons(item, category, [allowed, inactive, incompatible], links).Value);
    }

    [Theory]
    [InlineData("category")]
    [InlineData("item")]
    [InlineData("foreign_category")]
    public void GetAllowedAddons_RejectsUnavailableItemOrCategory(string reason)
    {
        var (category, item) = CreateItem();
        switch (reason)
        {
            case "category": category.Deactivate(Now); break;
            case "item": item.Deactivate(Now); break;
            case "foreign_category": category = CreateItem().Category; break;
        }

        Assert.Equal(ErrorType.Conflict, CatalogAddons.GetAllowedAddons(item, category, [], []).Error!.Type);
    }

    [Fact]
    public void GetAllowedAddons_NoCompatibilityReturnsEmptySelection()
    {
        var (category, item) = CreateItem();
        var addon = new Addon(EntityId.New(), "Extra shot", Money.Zero);

        Assert.Empty(CatalogAddons.GetAllowedAddons(item, category, [addon], []).Value);
    }
}
