namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

public class OrderLine
{
    private readonly List<OrderLineAddon> _addons = [];
    protected OrderLine()
    {
    }

    public OrderLine(EntityId id, MenuItem menuItem, SizeVariant sizeVariant, int quantity, IEnumerable<Addon>? addons = null, IReadOnlyCollection<MenuItemAddon>? compatibility = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        var snapshot = new CatalogPriceSnapshot(menuItem, sizeVariant);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        Id = id;
        MenuItemId = snapshot.MenuItemId;
        SizeVariantId = snapshot.SizeVariantId;
        ItemNameSnapshot = snapshot.ItemName;
        SizeNameSnapshot = snapshot.SizeName;
        UnitPriceSnapshot = snapshot.UnitPrice;
        Quantity = quantity;
        var selectedAddonIds = new HashSet<EntityId>();
        foreach (var addon in addons ?? [])
        {
            var addonSnapshot = new CatalogAddonSnapshot(menuItem, addon, compatibility ?? []);
            if (!selectedAddonIds.Add(addonSnapshot.AddonId))
            {
                throw new ArgumentException("Each add-on can be selected only once per drink.", nameof(addons));
            }

            _addons.Add(new OrderLineAddon(EntityId.New(), addonSnapshot));
        }
        LineTotal = new Money((UnitPriceSnapshot.Amount + _addons.Sum(addon => addon.AddonPriceSnapshot.Amount)) * quantity);
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId OrderId { get; protected set; } = null!;
    public EntityId? MenuItemId { get; protected set; }
    public EntityId? SizeVariantId { get; protected set; }
    public string ItemNameSnapshot { get; protected set; } = string.Empty;
    public string SizeNameSnapshot { get; protected set; } = string.Empty;
    public Money UnitPriceSnapshot { get; protected set; } = Money.Zero;
    public int Quantity { get; protected set; }
    public Money LineTotal { get; protected set; } = Money.Zero;
    public IReadOnlyCollection<OrderLineAddon> Addons => _addons.AsReadOnly();

    internal void AttachTo(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        OrderId = order.Id;
    }
}
