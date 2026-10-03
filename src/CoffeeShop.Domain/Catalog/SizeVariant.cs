namespace CoffeeShop.Domain.Catalog;

using CoffeeShop.Domain.Common;

public class SizeVariant
{
    protected SizeVariant()
    {
    }

    public SizeVariant(EntityId id, EntityId menuItemId, string sizeName, Money price, bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(menuItemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sizeName);
        ArgumentNullException.ThrowIfNull(price);

        Id = id;
        MenuItemId = menuItemId;
        SizeName = sizeName.Trim();
        Price = price;
        IsActive = isActive;
    }

    public EntityId Id { get; protected set; } = null!;

    public EntityId MenuItemId { get; protected set; } = null!;

    public string SizeName { get; protected set; } = string.Empty;

    public Money Price { get; protected set; } = null!;

    public bool IsActive { get; protected set; } = true;
}
