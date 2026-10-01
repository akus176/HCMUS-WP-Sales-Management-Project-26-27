namespace CoffeeShop.Domain.Catalog;

using CoffeeShop.Domain.Common;

public class MenuItemAddon
{
    protected MenuItemAddon()
    {
    }

    public MenuItemAddon(EntityId menuItemId, EntityId addonId)
    {
        ArgumentNullException.ThrowIfNull(menuItemId);
        ArgumentNullException.ThrowIfNull(addonId);

        MenuItemId = menuItemId;
        AddonId = addonId;
    }

    public EntityId MenuItemId { get; protected set; } = null!;

    public EntityId AddonId { get; protected set; } = null!;
}
