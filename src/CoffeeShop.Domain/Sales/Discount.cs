namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class Discount
{
    protected Discount()
    {
    }

    internal Discount(EntityId id, EntityId orderId, string type, decimal value, string reason,
        EntityId requestedBy, UtcTimestamp requestedAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(orderId);
        ArgumentNullException.ThrowIfNull(requestedBy);
        ArgumentNullException.ThrowIfNull(requestedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (type is not (DiscountType.Fixed or DiscountType.Percentage))
        {
            throw new ArgumentException("Discount type must be fixed or percentage.", nameof(type));
        }

        if (value <= 0 || value > Money.MaximumAmount || decimal.Round(value, 2) != value
            || (type == DiscountType.Percentage && value > 100m))
        {
            throw new ArgumentOutOfRangeException(nameof(value),
                "Discount value must be positive with at most two decimal places; percentages cannot exceed 100.");
        }

        Id = id;
        OrderId = orderId;
        Type = type;
        Value = value;
        Reason = reason.Trim();
        RequestedBy = requestedBy;
        RequestedAt = requestedAt;
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId OrderId { get; protected set; } = null!;
    public string Type { get; protected set; } = string.Empty;
    public decimal Value { get; protected set; }
    public string Reason { get; protected set; } = string.Empty;
    public string Status { get; protected set; } = DiscountStatus.Pending;
    public EntityId RequestedBy { get; protected set; } = null!;
    public UtcTimestamp RequestedAt { get; protected set; } = null!;
    public EntityId? ApprovedBy { get; protected set; }
    public UtcTimestamp? ApprovedAt { get; protected set; }
    public Money? ComputedAmount { get; protected set; }
    public EntityId? RejectedBy { get; protected set; }
    public UtcTimestamp? RejectedAt { get; protected set; }

    public Money CalculateAmount(Money payableTotal)
    {
        ArgumentNullException.ThrowIfNull(payableTotal);
        // Like loyalty, round down so fractional cents never increase the discount.
        var amount = Type == DiscountType.Fixed
            ? Value
            : decimal.Floor(payableTotal.Amount * Value) / 100m;
        if (amount <= 0 || amount > payableTotal.Amount)
        {
            throw new ArgumentOutOfRangeException(nameof(payableTotal),
                "The discount must be at least 0.01 and cannot exceed the current payable total.");
        }

        return new Money(amount);
    }

    internal void Approve(Money amount, EntityId actorId, UtcTimestamp changedAt)
    {
        ValidateDecision(actorId, changedAt);
        ComputedAmount = amount;
        ApprovedBy = actorId;
        ApprovedAt = changedAt;
        Status = DiscountStatus.Approved;
    }

    internal void Reject(EntityId actorId, UtcTimestamp changedAt)
    {
        ValidateDecision(actorId, changedAt);
        RejectedBy = actorId;
        RejectedAt = changedAt;
        Status = DiscountStatus.Rejected;
    }

    internal void EnsurePending()
    {
        if (Status != DiscountStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending discount can be approved or rejected.");
        }
    }

    private void ValidateDecision(EntityId actorId, UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(changedAt);
        EnsurePending();
        if (changedAt.Value < RequestedAt.Value)
        {
            throw new ArgumentException("A discount decision cannot predate its request.", nameof(changedAt));
        }
    }
}
