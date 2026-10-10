namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class DeliveryStatusHistory
{
    protected DeliveryStatusHistory() { }

    public DeliveryStatusHistory(EntityId id, EntityId deliveryId, string fromStatus, string toStatus, EntityId actorId, UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(deliveryId);
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(changedAt);
        if (!DeliveryStatus.IsKnown(fromStatus) || !DeliveryStatus.IsKnown(toStatus)) throw new ArgumentException("Delivery history contains an unknown status.");
        Id = id; DeliveryId = deliveryId; FromStatus = fromStatus; ToStatus = toStatus; ActorId = actorId; ChangedAt = changedAt;
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId DeliveryId { get; protected set; } = null!;
    public string FromStatus { get; protected set; } = string.Empty;
    public string ToStatus { get; protected set; } = string.Empty;
    public EntityId ActorId { get; protected set; } = null!;
    public UtcTimestamp ChangedAt { get; protected set; } = null!;
}
