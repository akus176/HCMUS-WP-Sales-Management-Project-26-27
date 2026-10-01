namespace CoffeeShop.Domain.Inventory;

using CoffeeShop.Domain.Common;

public class Supplier
{
    public Supplier(
        EntityId id,
        string name,
        string phone,
        UtcTimestamp createdAt,
        UtcTimestamp updatedAt,
        string? email = null,
        string? address = null,
        bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        ArgumentNullException.ThrowIfNull(createdAt);
        ArgumentNullException.ThrowIfNull(updatedAt);

        Id = id;
        Name = name.Trim();
        Phone = phone.Trim();
        Email = email?.Trim().ToLowerInvariant();
        Address = address?.Trim();
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public EntityId Id { get; protected set; } = null!;

    public string Name { get; protected set; } = string.Empty;

    public string Phone { get; protected set; } = string.Empty;

    public string? Email { get; protected set; }

    public string? Address { get; protected set; }

    public bool IsActive { get; protected set; } = true;

    public UtcTimestamp CreatedAt { get; protected set; } = null!;

    public UtcTimestamp UpdatedAt { get; protected set; } = null!;
}