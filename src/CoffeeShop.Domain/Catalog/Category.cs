namespace CoffeeShop.Domain.Catalog;

using CoffeeShop.Domain.Common;

public class Category
{
    protected Category()
    {
    }

    public Category(EntityId id, string name, UtcTimestamp createdAt, UtcTimestamp updatedAt, int displayOrder = 0, bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(createdAt);
        ArgumentNullException.ThrowIfNull(updatedAt);

        Id = id;
        Name = name.Trim();
        DisplayOrder = displayOrder;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public EntityId Id { get; protected set; } = null!;

    public string Name { get; protected set; } = string.Empty;

    public int DisplayOrder { get; protected set; }

    public bool IsActive { get; protected set; } = true;

    public UtcTimestamp CreatedAt { get; protected set; } = null!;

    public UtcTimestamp UpdatedAt { get; protected set; } = null!;

    public void Rename(string name, UtcTimestamp changedAt)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(changedAt);

        Name = name.Trim();
        UpdatedAt = changedAt;
    }

    public void Activate(UtcTimestamp changedAt) => SetActive(true, changedAt);

    public void Deactivate(UtcTimestamp changedAt) => SetActive(false, changedAt);

    private void SetActive(bool isActive, UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(changedAt);
        if (IsActive == isActive)
        {
            throw new InvalidOperationException(isActive ? "Category is already active." : "Category is already inactive.");
        }

        IsActive = isActive;
        UpdatedAt = changedAt;
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Trim().Length > 80)
        {
            throw new ArgumentException("Category name cannot exceed 80 characters.", nameof(name));
        }
    }
}
