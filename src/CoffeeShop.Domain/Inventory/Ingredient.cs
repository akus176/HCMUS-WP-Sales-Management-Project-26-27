namespace CoffeeShop.Domain.Inventory;

using CoffeeShop.Domain.Common;

public class Ingredient
{
    public const decimal MaximumQuantity = MeasuredQuantity.MaximumValue;

    public Ingredient(
        EntityId id,
        string name,
        string baseUnit,
        decimal currentBalance,
        decimal minimumLevel,
        Money unitCost,
        UtcTimestamp createdAt,
        UtcTimestamp updatedAt,
        bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUnit);
        ArgumentNullException.ThrowIfNull(unitCost);
        ArgumentNullException.ThrowIfNull(createdAt);
        ArgumentNullException.ThrowIfNull(updatedAt);

        ValidateQuantity(currentBalance, nameof(currentBalance));
        ValidateQuantity(minimumLevel, nameof(minimumLevel));

        Id = id;
        Name = name.Trim();
        BaseUnit = baseUnit.Trim().ToLowerInvariant();
        CurrentBalance = currentBalance;
        MinimumLevel = minimumLevel;
        UnitCost = unitCost;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public EntityId Id { get; protected set; } = null!;

    public string Name { get; protected set; } = string.Empty;

    public string BaseUnit { get; protected set; } = string.Empty;

    public decimal CurrentBalance { get; protected set; }

    public decimal MinimumLevel { get; protected set; }

    public Money UnitCost { get; protected set; } = null!;

    public bool IsActive { get; protected set; } = true;

    public UtcTimestamp CreatedAt { get; protected set; } = null!;

    public UtcTimestamp UpdatedAt { get; protected set; } = null!;

    public bool IsLowStock => CurrentBalance <= MinimumLevel;

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

        if (value > MaximumQuantity)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Ingredient quantity cannot exceed the maximum of {MaximumQuantity}.");
        }
    }
}
