namespace CoffeeShop.Domain.Inventory;

using CoffeeShop.Domain.Common;

public class Ingredient
{
    protected Ingredient()
    {
    }

    public Ingredient(EntityId id, string name, string baseUnit, UtcTimestamp createdAt, UtcTimestamp updatedAt, decimal availableQuantity = 0, decimal reorderThreshold = 0, bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(createdAt);
        ArgumentNullException.ThrowIfNull(updatedAt);
        var balance = new MeasuredQuantity(availableQuantity, baseUnit);
        var threshold = new MeasuredQuantity(reorderThreshold, baseUnit);

        Id = id;
        Name = name.Trim();
        BaseUnit = balance.Unit;
        AvailableQuantity = balance.Value;
        ReorderThreshold = threshold.Value;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public EntityId Id { get; protected set; } = null!;

    public string Name { get; protected set; } = string.Empty;

    public string BaseUnit { get; protected set; } = string.Empty;

    public decimal AvailableQuantity { get; protected set; }

    public decimal ReorderThreshold { get; protected set; }

    public bool IsActive { get; protected set; } = true;

    public UtcTimestamp CreatedAt { get; protected set; } = null!;

    public UtcTimestamp UpdatedAt { get; protected set; } = null!;

    public bool IsLowStock => AvailableQuantity <= ReorderThreshold;

    private static void ValidateQuantity(decimal value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Ingredient quantity cannot be negative.");
        }

        if (decimal.Round(value, 3) != value)
        {
            throw new ArgumentException("Ingredient quantity cannot have more than three decimal places.", paramName);
        }

        if (value > MeasuredQuantity.MaximumValue)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Ingredient quantity cannot exceed the maximum of {MeasuredQuantity.MaximumValue}.");
        }
    }
}
