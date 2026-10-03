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

    /// <summary>
    /// Increases the available balance by a positive amount.
    /// </summary>
    public void Receive(decimal amount, UtcTimestamp occurredAt)
    {
        ArgumentNullException.ThrowIfNull(occurredAt);
        ValidatePositiveAmount(amount);

        var newBalance = new MeasuredQuantity(AvailableQuantity + amount, BaseUnit);
        AvailableQuantity = newBalance.Value;
        UpdatedAt = occurredAt;
    }

    /// <summary>
    /// Decreases the available balance by a positive amount.
    /// The balance can never become negative.
    /// </summary>
    public void Consume(decimal amount, UtcTimestamp occurredAt)
    {
        ArgumentNullException.ThrowIfNull(occurredAt);
        ValidatePositiveAmount(amount);

        if (amount > AvailableQuantity)
        {
            throw new InvalidOperationException($"Cannot consume {amount} {BaseUnit}; only {AvailableQuantity} {BaseUnit} available.");
        }

        var newBalance = new MeasuredQuantity(AvailableQuantity - amount, BaseUnit);
        AvailableQuantity = newBalance.Value;
        UpdatedAt = occurredAt;
    }

    /// <summary>
    /// Applies a signed balance change. A positive delta increases the balance,
    /// a negative delta decreases it. Zero deltas are rejected because every
    /// stock change must be observable.
    /// </summary>
    public void Adjust(decimal delta, UtcTimestamp occurredAt)
    {
        ArgumentNullException.ThrowIfNull(occurredAt);

        if (delta == 0)
        {
            throw new ArgumentException("Stock adjustment delta cannot be zero.", nameof(delta));
        }

        var newBalanceValue = AvailableQuantity + delta;
        var newBalance = new MeasuredQuantity(newBalanceValue, BaseUnit);
        AvailableQuantity = newBalance.Value;
        UpdatedAt = occurredAt;
    }

    /// <summary>
    /// Sets the reorder threshold used by the low-stock boundary rule.
    /// </summary>
    public void SetReorderThreshold(decimal value, UtcTimestamp occurredAt)
    {
        ArgumentNullException.ThrowIfNull(occurredAt);
        var threshold = new MeasuredQuantity(value, BaseUnit);
        ReorderThreshold = threshold.Value;
        UpdatedAt = occurredAt;
    }

    /// <summary>
    /// Deactivates the ingredient. Deactivated ingredients keep their balance
    /// and history but cannot participate in new stock operations.
    /// </summary>
    public void Deactivate(UtcTimestamp occurredAt)
    {
        ArgumentNullException.ThrowIfNull(occurredAt);
        IsActive = false;
        UpdatedAt = occurredAt;
    }

    /// <summary>
    /// Reactivates a previously deactivated ingredient.
    /// </summary>
    public void Activate(UtcTimestamp occurredAt)
    {
        ArgumentNullException.ThrowIfNull(occurredAt);
        IsActive = true;
        UpdatedAt = occurredAt;
    }

    /// <summary>
    /// Determines whether a quantity expressed in the given unit can be
    /// applied to this ingredient. Compatible when the unit belongs to the
    /// same family as the ingredient's base unit.
    /// </summary>
    public bool CanAcceptUnit(string unit) => IngredientUnit.AreCompatible(BaseUnit, unit);

    /// <summary>
    /// Converts a quantity from the given unit into the ingredient's base
    /// unit, or null when the unit is incompatible or unknown.
    /// </summary>
    public decimal? ConvertToBaseUnit(decimal value, string unit) =>
        IngredientUnit.Convert(value, unit, BaseUnit);

    private static void ValidatePositiveAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Stock amount must be positive.", nameof(amount));
        }

        if (decimal.Round(amount, 3) != amount)
        {
            throw new ArgumentException("Stock amount cannot have more than three decimal places.", nameof(amount));
        }

        if (amount > MeasuredQuantity.MaximumValue)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, $"Stock amount cannot exceed the maximum of {MeasuredQuantity.MaximumValue}.");
        }
    }
}
