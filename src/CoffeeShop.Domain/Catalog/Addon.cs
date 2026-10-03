namespace CoffeeShop.Domain.Catalog;

using CoffeeShop.Domain.Common;

public class Addon
{
    protected Addon()
    {
    }

    public Addon(EntityId id, string name, Money price, bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(price);

        Id = id;
        Name = name.Trim();
        Price = price;
        IsActive = isActive;
    }

    public EntityId Id { get; protected set; } = null!;

    public string Name { get; protected set; } = string.Empty;

    public Money Price { get; protected set; } = null!;

    public bool IsActive { get; protected set; } = true;
}
