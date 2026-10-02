namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

public class OrderLine
{
    private readonly List<OrderLineAddon> _addons = [];
    protected OrderLine()
    {
    }

    public OrderLine(EntityId id, MenuItem menuItem, SizeVariant sizeVariant, int quantity, IEnumerable<Addon>? addons = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(menuItem);
        ArgumentNullException.ThrowIfNull(sizeVariant);
        if (!menuItem.IsActive || !sizeVariant.IsActive || sizeVariant.MenuItemId != menuItem.Id)
        {
            throw new ArgumentException("The selected menu item and size variant must be active and compatible.");
        }
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        Id = id;
        MenuItemId = menuItem.Id;
        SizeVariantId = sizeVariant.Id;
        ItemNameSnapshot = menuItem.Name;
        SizeNameSnapshot = sizeVariant.SizeName;
        UnitPriceSnapshot = sizeVariant.Price;
        Quantity = quantity;
        foreach (var addon in addons ?? [])
        {
            _addons.Add(new OrderLineAddon(EntityId.New(), addon));
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
