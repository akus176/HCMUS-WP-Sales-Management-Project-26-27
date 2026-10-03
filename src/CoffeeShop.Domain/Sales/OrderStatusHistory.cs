namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class OrderStatusHistory
{
    protected OrderStatusHistory()
    {
    }

    public OrderStatusHistory(EntityId id, EntityId orderId, string? fromStatus, string toStatus, EntityId changedBy, UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(orderId);
        ArgumentNullException.ThrowIfNull(changedBy);
        ArgumentNullException.ThrowIfNull(changedAt);
        if (!OrderStatus.IsKnown(toStatus) || (fromStatus is not null && !OrderStatus.IsKnown(fromStatus)))
        {
            throw new ArgumentException("Order history contains an unknown status.", nameof(toStatus));
        }

        Id = id;
        OrderId = orderId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedBy = changedBy;
        ChangedAt = changedAt;
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId OrderId { get; protected set; } = null!;
    public string? FromStatus { get; protected set; }
    public string ToStatus { get; protected set; } = string.Empty;
    public EntityId ChangedBy { get; protected set; } = null!;
    public UtcTimestamp ChangedAt { get; protected set; } = null!;
}
