namespace CoffeeShop.Domain.Inventory;

using CoffeeShop.Domain.Common;

public class PurchaseReceiptLine
{
    public PurchaseReceiptLine(
        EntityId id,
        EntityId ingredientId,
        decimal quantity,
        Money unitCost)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(ingredientId);
        ArgumentNullException.ThrowIfNull(unitCost);

        ValidateQuantity(quantity);

        Id = id;
        IngredientId = ingredientId;
        Quantity = quantity;
        UnitCost = unitCost;
    }

    // Parameterless constructor for EF Core
    protected PurchaseReceiptLine()
    {
    }

    public EntityId Id { get; protected set; } = null!;

    public EntityId IngredientId { get; protected set; } = null!;

    public decimal Quantity { get; protected set; }

    public Money UnitCost { get; protected set; } = null!;

    public EntityId PurchaseReceiptId { get; protected set; } = null!;

    public PurchaseReceipt PurchaseReceipt { get; protected set; } = null!;

    private static void ValidateQuantity(decimal value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Receipt line quantity must be positive.");
        }

        if (decimal.Round(value, 3) != value)
        {
            throw new ArgumentException("Receipt line quantity cannot have more than three decimal places.", nameof(value));
        }

        if (value > MeasuredQuantity.MaximumValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Receipt line quantity cannot exceed the maximum of {MeasuredQuantity.MaximumValue}.");
        }
    }
}
