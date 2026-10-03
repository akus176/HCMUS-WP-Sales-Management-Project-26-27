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
        ValidateSizeName(sizeName);
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

    public void Update(string sizeName, Money price)
    {
        ValidateSizeName(sizeName);
        ArgumentNullException.ThrowIfNull(price);

        SizeName = sizeName.Trim();
        Price = price;
    }

    public void Activate() => SetActive(true);

    public void Deactivate() => SetActive(false);

    public bool IsAvailableForOrdering(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return IsActive && item.IsActive && MenuItemId == item.Id;
    }

    private void SetActive(bool isActive)
    {
        if (IsActive == isActive)
        {
            throw new InvalidOperationException(isActive ? "Size variant is already active." : "Size variant is already inactive.");
        }

        IsActive = isActive;
    }

    private static void ValidateSizeName(string sizeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sizeName);
        if (sizeName.Trim().Length > 40)
        {
            throw new ArgumentException("Size name cannot exceed 40 characters.", nameof(sizeName));
        }
    }
}
