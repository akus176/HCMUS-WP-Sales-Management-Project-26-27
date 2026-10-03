namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

public class OrderLineAddon
{
    protected OrderLineAddon()
    {
    }

    public OrderLineAddon(EntityId id, CatalogAddonSnapshot addon)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(addon);
        Id = id;
        AddonId = addon.AddonId;
        AddonNameSnapshot = addon.Name;
        AddonPriceSnapshot = addon.UnitPrice;
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId OrderLineId { get; protected set; } = null!;
    public EntityId? AddonId { get; protected set; }
    public string AddonNameSnapshot { get; protected set; } = string.Empty;
    public Money AddonPriceSnapshot { get; protected set; } = Money.Zero;
}
