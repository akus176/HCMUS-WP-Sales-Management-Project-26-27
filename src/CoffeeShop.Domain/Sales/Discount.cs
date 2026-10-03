namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class Discount
{
    protected Discount()
    {
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId OrderId { get; protected set; } = null!;
    public string Type { get; protected set; } = string.Empty;
    public decimal Value { get; protected set; }
    public string Status { get; protected set; } = "pending";
    public EntityId RequestedBy { get; protected set; } = null!;
    public UtcTimestamp RequestedAt { get; protected set; } = null!;
    public EntityId? ApprovedBy { get; protected set; }
    public UtcTimestamp? ApprovedAt { get; protected set; }
    public Money? ComputedAmount { get; protected set; }
}
