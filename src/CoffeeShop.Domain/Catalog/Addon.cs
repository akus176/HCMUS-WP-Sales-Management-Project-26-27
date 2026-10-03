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
        ValidateName(name);
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

    public void Update(string name, Money price)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(price);

        Name = name.Trim();
        Price = price;
    }

    public void Activate() => SetActive(true);

    public void Deactivate() => SetActive(false);

    public bool IsAvailableForOrdering(MenuItem item, IEnumerable<MenuItemAddon> compatibility)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(compatibility);
        return IsActive && item.IsActive && compatibility.Any(link => link.MenuItemId == item.Id && link.AddonId == Id);
    }

    private void SetActive(bool isActive)
    {
        if (IsActive == isActive)
        {
            throw new InvalidOperationException(isActive ? "Add-on is already active." : "Add-on is already inactive.");
        }

        IsActive = isActive;
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Trim().Length > 80)
        {
            throw new ArgumentException("Add-on name cannot exceed 80 characters.", nameof(name));
        }
    }
}
