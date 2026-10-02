namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class OrderLine
{
    protected OrderLine()
    {
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
}
