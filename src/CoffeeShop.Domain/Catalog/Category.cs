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
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
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
}
