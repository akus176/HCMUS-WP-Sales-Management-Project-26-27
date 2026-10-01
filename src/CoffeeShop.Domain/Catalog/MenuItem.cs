namespace CoffeeShop.Domain.Catalog;

using CoffeeShop.Domain.Common;

public class MenuItem
{
    protected MenuItem()
    {
    }

    public MenuItem(EntityId id, EntityId categoryId, string name, UtcTimestamp createdAt, UtcTimestamp updatedAt, string? description = null, bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(categoryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(createdAt);
        ArgumentNullException.ThrowIfNull(updatedAt);

        Id = id;
        CategoryId = categoryId;
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
}
