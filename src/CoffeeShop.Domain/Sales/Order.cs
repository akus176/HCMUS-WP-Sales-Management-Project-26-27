namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class Order
{
    protected Order()
    {
    }

    public EntityId Id { get; protected set; } = null!;
    public string OrderNumber { get; protected set; } = string.Empty;
    public string Channel { get; protected set; } = string.Empty;
    public string Status { get; protected set; } = "draft";
    public EntityId? CustomerId { get; protected set; }
    public EntityId? TableId { get; protected set; }
    public Money Subtotal { get; protected set; } = Money.Zero;
    public Money DiscountTotal { get; protected set; } = Money.Zero;
    public Money Total { get; protected set; } = Money.Zero;
    public Money PaidAmount { get; protected set; } = Money.Zero;
    public int LoyaltyPointsRedeemed { get; protected set; }
    public Money LoyaltyDiscountAmount { get; protected set; } = Money.Zero;
    public EntityId CreatedBy { get; protected set; } = null!;
    public UtcTimestamp CreatedAt { get; protected set; } = null!;
    public UtcTimestamp? ConfirmedAt { get; protected set; }
    public UtcTimestamp? CompletedAt { get; protected set; }
    public UtcTimestamp? CancelledAt { get; protected set; }
    public string? CancelReason { get; protected set; }
    public uint Version { get; protected set; }
}
