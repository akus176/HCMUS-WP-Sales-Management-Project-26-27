namespace CoffeeShop.Domain.Catalog;

using CoffeeShop.Domain.Common;

// Simple add-ons allow one selection of each add-on per drink, across all sizes of an item.
public sealed record CatalogAddonSnapshot
{
    public CatalogAddonSnapshot(MenuItem item, Addon addon, IEnumerable<MenuItemAddon> compatibility)
    {
        ArgumentNullException.ThrowIfNull(addon);
        if (!addon.IsAvailableForOrdering(item, compatibility))
        {
            throw new ArgumentException("The selected add-on must be active and compatible with the menu item.", nameof(addon));
        }

        MenuItemId = item.Id;
        AddonId = addon.Id;
        Name = addon.Name;
        UnitPrice = addon.Price;
    }

    public EntityId MenuItemId { get; }

    public EntityId AddonId { get; }

    public string Name { get; }

    public Money UnitPrice { get; }

    public int MaximumQuantityPerDrink => 1;
}
