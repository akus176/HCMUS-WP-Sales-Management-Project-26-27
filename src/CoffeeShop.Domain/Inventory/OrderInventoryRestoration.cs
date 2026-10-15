namespace CoffeeShop.Domain.Inventory;

using CoffeeShop.Domain.Common;

/// <summary>The order identifier is the durable idempotency key for restoration.</summary>
public sealed class OrderInventoryRestoration
{
    public OrderInventoryRestoration(EntityId orderId, UtcTimestamp restoredAt, EntityId restoredBy)
    {
        ArgumentNullException.ThrowIfNull(orderId);
        ArgumentNullException.ThrowIfNull(restoredAt);
        ArgumentNullException.ThrowIfNull(restoredBy);
        OrderId = orderId;
        RestoredAt = restoredAt;
        RestoredBy = restoredBy;
    }

    public EntityId OrderId { get; private set; }
    public UtcTimestamp RestoredAt { get; private set; }
    public EntityId RestoredBy { get; private set; }
}
