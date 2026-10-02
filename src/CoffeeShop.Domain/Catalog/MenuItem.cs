namespace CoffeeShop.Domain.Catalog;

using CoffeeShop.Domain.Common;

public class MenuItem
{
    protected MenuItem()
    {
    }

    public MenuItem(EntityId id, Category category, string name, UtcTimestamp createdAt, UtcTimestamp updatedAt, string? description = null, bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        ValidateCategory(category);
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(createdAt);
        ArgumentNullException.ThrowIfNull(updatedAt);

        Id = id;
        CategoryId = category.Id;
        Name = name.Trim();
        Description = description;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public EntityId Id { get; protected set; } = null!;

    public EntityId CategoryId { get; protected set; } = null!;

    public string Name { get; protected set; } = string.Empty;

    public string? Description { get; protected set; }

    public bool IsActive { get; protected set; } = true;

    public UtcTimestamp CreatedAt { get; protected set; } = null!;

    public UtcTimestamp UpdatedAt { get; protected set; } = null!;

    public void Update(string name, string? description, Category category, UtcTimestamp changedAt)
    {
        ValidateName(name);
        ValidateCategory(category);
        ArgumentNullException.ThrowIfNull(changedAt);

        Name = name.Trim();
        Description = description;
        CategoryId = category.Id;
        UpdatedAt = changedAt;
    }

    public void Activate(Category category, UtcTimestamp changedAt)
    {
        ValidateCategory(category);
        if (CategoryId != category.Id)
        {
            throw new ArgumentException("Category does not match the menu item's category.", nameof(category));
        }

        SetActive(true, changedAt);
    }

    public void Deactivate(UtcTimestamp changedAt) => SetActive(false, changedAt);

    public bool IsAvailableForOrdering(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return IsActive && category.IsActive && CategoryId == category.Id;
    }

    private void SetActive(bool isActive, UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(changedAt);
        if (IsActive == isActive)
        {
            throw new InvalidOperationException(isActive ? "Menu item is already active." : "Menu item is already inactive.");
        }

        IsActive = isActive;
        UpdatedAt = changedAt;
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Trim().Length > 120)
        {
            throw new ArgumentException("Menu item name cannot exceed 120 characters.", nameof(name));
        }
    }

    private static void ValidateCategory(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (!category.IsActive)
        {
            throw new InvalidOperationException("Menu items require an active category.");
        }
    }
}
