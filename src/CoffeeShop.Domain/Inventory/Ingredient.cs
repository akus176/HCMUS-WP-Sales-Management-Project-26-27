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
        var balance = ValidateQuantity(availableQuantity, baseUnit, nameof(availableQuantity));
        var threshold = ValidateQuantity(reorderThreshold, baseUnit, nameof(reorderThreshold));

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

    /// <summary>
    /// The current stock status derived from the balance and threshold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Low-stock boundary rule (authoritative):</b> an ingredient is
    /// <see cref="StockStatus.LowStock"/> when its available quantity is
    /// <b>less than or equal to</b> its reorder threshold — the threshold
    /// itself counts as low stock, because reaching the reorder point is
    /// exactly when a purchase should be triggered.
    /// </para>
    /// <para>
    /// A <b>deactivated</b> ingredient never reports low stock regardless of
    /// its balance: it is out of operation, so it must not appear on reorder
    /// lists or dashboards. Reactivating it restores normal status evaluation.
    /// </para>
    /// </remarks>
    public StockStatus Status =>
        !IsActive || AvailableQuantity <= ReorderThreshold
            ? StockStatus.LowStock
            : StockStatus.Ok;

    /// <summary>
    /// Convenience flag equivalent to <see cref="Status"/> being
    /// <see cref="StockStatus.LowStock"/>.
    /// </summary>
    public bool IsLowStock => Status == StockStatus.LowStock;

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
        var newBalance = ValidateQuantity(newBalanceValue, BaseUnit, nameof(delta));
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

    private static MeasuredQuantity ValidateQuantity(decimal value, string unit, string paramName)
    {
        try
        {
            return new MeasuredQuantity(value, unit);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            throw new ArgumentException(exception.Message, paramName, exception);
        }
    }

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
