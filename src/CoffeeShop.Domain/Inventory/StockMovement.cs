namespace CoffeeShop.Domain.Inventory;

using CoffeeShop.Domain.Common;

public class StockMovement
{
    public StockMovement(
        EntityId id,
        EntityId ingredientId,
        StockMovementType type,
        decimal quantityDelta,
        EntityId actorId,
        string reason,
        UtcTimestamp occurredAt,
        EntityId? purchaseReceiptId = null,
        EntityId? orderId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(ingredientId);
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(occurredAt);

        ValidateQuantityDelta(quantityDelta);

        if (type == StockMovementType.Receipt && purchaseReceiptId is null)
        {
            throw new ArgumentException("A receipt movement must reference its purchase receipt.", nameof(purchaseReceiptId));
        }

        if (type is StockMovementType.OrderConsumption or StockMovementType.Restoration && orderId is null)
        {
            throw new ArgumentException($"An {type} movement must reference its order.", nameof(orderId));
        }

        if (type == StockMovementType.Adjustment && string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("An adjustment movement requires a mandatory reason.", nameof(reason));
        }

        Id = id;
        IngredientId = ingredientId;
        Type = type;
        QuantityDelta = quantityDelta;
        ActorId = actorId;
        Reason = reason.Trim();
        OccurredAt = occurredAt;
        PurchaseReceiptId = purchaseReceiptId;
        OrderId = orderId;
    }

    public EntityId Id { get; protected set; } = null!;

    public EntityId IngredientId { get; protected set; } = null!;

    public StockMovementType Type { get; protected set; }

    public decimal QuantityDelta { get; protected set; }

    public EntityId ActorId { get; protected set; } = null!;

    public string Reason { get; protected set; } = string.Empty;

    public UtcTimestamp OccurredAt { get; protected set; } = null!;

    public EntityId? PurchaseReceiptId { get; protected set; }

    public EntityId? OrderId { get; protected set; }

    private static void ValidateQuantityDelta(decimal value)
    {
        if (value == 0)
        {
            throw new ArgumentException("Stock movement quantity delta cannot be zero.", nameof(value));
        }

        if (decimal.Round(value, 3) != value)
        {
            throw new ArgumentException("Stock movement quantity delta cannot have more than three decimal places.", nameof(value));
        }

        if (Math.Abs(value) > MeasuredQuantity.MaximumValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Stock movement quantity delta cannot exceed the maximum of {MeasuredQuantity.MaximumValue}.");
        }
    }
}
