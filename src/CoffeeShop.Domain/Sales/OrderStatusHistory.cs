namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class OrderStatusHistory
{
    protected OrderStatusHistory()
    {
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId OrderId { get; protected set; } = null!;
    public string? FromStatus { get; protected set; }
    public string ToStatus { get; protected set; } = string.Empty;
    public EntityId ChangedBy { get; protected set; } = null!;
    public UtcTimestamp ChangedAt { get; protected set; } = null!;
}
