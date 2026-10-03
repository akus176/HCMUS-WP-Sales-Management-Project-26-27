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

    /// <summary>
    /// Updates the supplier's contact data. Deactivation history is preserved:
    /// updating never reactivates a deactivated supplier.
    /// </summary>
    public void Update(string name, string phone, string? email, string? address, UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(changedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        Name = name.Trim();
        Phone = phone.Trim();
        Email = email?.Trim().ToLowerInvariant();
        Address = address?.Trim();
        UpdatedAt = changedAt;
    }

    /// <summary>
    /// Deactivates the supplier. Historical receipts keep their reference;
    /// deactivation only prevents the supplier from being selected for new
    /// receipts.
    /// </summary>
    public void Deactivate(UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(changedAt);
        IsActive = false;
        UpdatedAt = changedAt;
    }

    /// <summary>
    /// Reactivates a previously deactivated supplier.
    /// </summary>
    public void Activate(UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(changedAt);
        IsActive = true;
        UpdatedAt = changedAt;
    }
}
