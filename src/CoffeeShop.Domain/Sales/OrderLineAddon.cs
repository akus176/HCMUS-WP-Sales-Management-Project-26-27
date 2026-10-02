namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

public class OrderLineAddon
{
    protected OrderLineAddon()
    {
    }

    public OrderLineAddon(EntityId id, Addon addon)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(addon);
        if (!addon.IsActive)
        {
            throw new ArgumentException("The selected add-on must be active.", nameof(addon));
        }

        Id = id;
        AddonId = addon.Id;
        AddonNameSnapshot = addon.Name;
        AddonPriceSnapshot = addon.Price;
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId OrderLineId { get; protected set; } = null!;
    public EntityId? AddonId { get; protected set; }
    public string AddonNameSnapshot { get; protected set; } = string.Empty;
    public Money AddonPriceSnapshot { get; protected set; } = Money.Zero;
}
