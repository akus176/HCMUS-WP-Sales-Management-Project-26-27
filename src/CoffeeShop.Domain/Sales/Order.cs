namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class Order
{
    private readonly List<OrderLine> _lines = [];
    private readonly List<OrderStatusHistory> _statusHistory = [];
    private readonly List<Payment> _payments = [];
    private readonly List<Discount> _discounts = [];
    protected Order()
    {
    }

    public Order(EntityId id, string orderNumber, string channel, EntityId createdBy, UtcTimestamp createdAt, IEnumerable<OrderLine> lines, EntityId? customerId = null, EntityId? tableId = null, Delivery? delivery = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);
        ArgumentNullException.ThrowIfNull(createdBy);
        ArgumentNullException.ThrowIfNull(createdAt);
        ArgumentNullException.ThrowIfNull(lines);
        if (channel is not ("dine_in" or "takeaway" or "delivery"))
        {
            throw new ArgumentException("Order channel must be dine_in, takeaway, or delivery.", nameof(channel));
        }
        var selectedLines = lines.ToList();
        if (selectedLines.Count == 0)
        {
            throw new ArgumentException("An order requires at least one line.", nameof(lines));
        }
        if (channel == "dine_in" && tableId is null)
        {
            throw new ArgumentException("Dine-in orders require a table.", nameof(tableId));
        }
        if (channel == "delivery" && delivery is null)
        {
            throw new ArgumentException("Delivery orders require delivery details.", nameof(delivery));
        }
        if (channel != "delivery" && delivery is not null)
        {
            throw new ArgumentException("Only delivery orders can contain delivery details.", nameof(delivery));
        }
        if (delivery is not null && delivery.OrderId != id)
        {
            throw new ArgumentException("Delivery details must belong to this order.", nameof(delivery));
        }
        if (delivery is not null && delivery.CustomerAddressId is not null && customerId is null)
        {
            throw new ArgumentException("Delivery orders referencing a customer address must have an attached customer.", nameof(delivery));
        }

        Id = id;
        OrderNumber = orderNumber.Trim();
        Channel = channel;
        CustomerId = customerId;
        TableId = tableId;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        _lines.AddRange(selectedLines);
        foreach (var line in _lines)
        {
            line.AttachTo(this);
        }
        Subtotal = new Money(_lines.Sum(line => line.LineTotal.Amount));
        Total = Subtotal;
    }

    public EntityId Id { get; protected set; } = null!;
    public string OrderNumber { get; protected set; } = string.Empty;
    public string Channel { get; protected set; } = string.Empty;
    public string Status { get; protected set; } = OrderStatus.Draft;
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
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    public IReadOnlyCollection<OrderStatusHistory> StatusHistory => _statusHistory.AsReadOnly();
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();
    public IReadOnlyCollection<Discount> Discounts => _discounts.AsReadOnly();
    public Money OutstandingBalance => new(decimal.Max(0m, Total.Amount - PaidAmount.Amount));
    public bool IsFullyPaid => PaidAmount.Amount >= Total.Amount;

    public OrderStatusHistory TransitionTo(string targetStatus, EntityId actorId, UtcTimestamp changedAt, string? cancellationReason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetStatus);
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(changedAt);
        if (!OrderStatus.IsKnown(targetStatus))
        {
            throw new ArgumentException($"Unknown order status '{targetStatus}'.", nameof(targetStatus));
        }
        if (!CanTransitionTo(targetStatus))
        {
            throw new InvalidOperationException($"Order cannot transition from '{Status}' to '{targetStatus}'.");
        }
        if (targetStatus == OrderStatus.Cancelled && string.IsNullOrWhiteSpace(cancellationReason))
        {
            throw new ArgumentException("A cancellation reason is required.", nameof(cancellationReason));
        }

        if (targetStatus == OrderStatus.Completed && !IsFullyPaid)
        {
            throw new InvalidOperationException("An order cannot be completed while it has an unpaid balance.");
        }

        var history = new OrderStatusHistory(EntityId.New(), Id, Status, targetStatus, actorId, changedAt);
        Status = targetStatus;
        if (targetStatus == OrderStatus.Confirmed) ConfirmedAt = changedAt;
        if (targetStatus == OrderStatus.Completed) CompletedAt = changedAt;
        if (targetStatus == OrderStatus.Cancelled)
        {
            CancelledAt = changedAt;
            CancelReason = cancellationReason!.Trim();
        }
        _statusHistory.Add(history);
        return history;
    }

    public Payment RecordPayment(
        EntityId paymentId,
        string method,
        Money amount,
        string status,
        UtcTimestamp paidAt,
        EntityId recordedBy,
        string? reference = null)
    {
        if (Status is OrderStatus.Completed or OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Payments cannot be recorded for completed or cancelled orders.");
        }

        if (_payments.Any(payment => payment.Id == paymentId))
        {
            throw new InvalidOperationException("The payment identifier is already recorded for this order.");
        }

        var payment = new Payment(paymentId, Id, method, amount, status, paidAt, recordedBy, reference);
        _payments.Add(payment);
        RecalculatePaidAmount();
        return payment;
    }

    public void ApplyLoyaltyRedemption(int points, Money discount)
    {
        ArgumentNullException.ThrowIfNull(discount);
        if (Status is not (OrderStatus.Draft or OrderStatus.Confirmed))
        {
            throw new InvalidOperationException("Loyalty redemption requires a draft or confirmed order.");
        }

        if (points <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Redeemed points must be positive.");
        }

        if (discount.Amount <= 0 || discount.Amount > Total.Amount)
        {
            throw new ArgumentOutOfRangeException(nameof(discount), "Loyalty discount must be positive and cannot exceed the order total.");
        }

        if (LoyaltyPointsRedeemed != 0 || LoyaltyDiscountAmount.Amount != 0)
        {
            throw new InvalidOperationException("The order already has a loyalty redemption.");
        }

        LoyaltyPointsRedeemed = points;
        LoyaltyDiscountAmount = discount;
        Total = new Money(Total.Amount - discount.Amount);
    }

    public Discount RequestDiscount(EntityId discountId, string type, decimal value, string reason,
        EntityId actorId, UtcTimestamp requestedAt)
    {
        EnsureDiscountsAllowed();
        ArgumentNullException.ThrowIfNull(requestedAt);
        if (_discounts.Any(discount => discount.Id == discountId))
        {
            throw new InvalidOperationException("The discount identifier is already recorded for this order.");
        }

        if (requestedAt.Value < CreatedAt.Value)
        {
            throw new ArgumentException("A discount request cannot predate the order.", nameof(requestedAt));
        }

        var discount = new Discount(discountId, Id, type, value, reason, actorId, requestedAt);
        discount.CalculateAmount(Total);
        _discounts.Add(discount);
        return discount;
    }

    public Discount ApproveDiscount(EntityId discountId, EntityId actorId, UtcTimestamp approvedAt)
    {
        EnsureDiscountsAllowed();
        var discount = FindDiscount(discountId);
        discount.EnsurePending();
        // Recalculate at approval: prior approvals and loyalty may have changed the payable total.
        var amount = discount.CalculateAmount(Total);
        var total = new Money(Total.Amount - amount.Amount);
        var discountTotal = new Money(DiscountTotal.Amount + amount.Amount);
        discount.Approve(amount, actorId, approvedAt);
        DiscountTotal = discountTotal;
        Total = total;
        return discount;
    }

    public Discount RejectDiscount(EntityId discountId, EntityId actorId, UtcTimestamp rejectedAt)
    {
        EnsureDiscountsAllowed();
        var discount = FindDiscount(discountId);
        discount.Reject(actorId, rejectedAt);
        return discount;
    }

    private Discount FindDiscount(EntityId discountId)
    {
        ArgumentNullException.ThrowIfNull(discountId);
        return _discounts.SingleOrDefault(discount => discount.Id == discountId)
            ?? throw new ArgumentException("The discount must belong to this order.", nameof(discountId));
    }

    private void EnsureDiscountsAllowed()
    {
        if (Status is OrderStatus.Completed or OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Discounts cannot be changed for completed or cancelled orders.");
        }
    }

    private void RecalculatePaidAmount()
    {
        var amount = _payments
            .Where(payment => payment.CountsTowardPaidBalance)
            .Sum(payment => payment.Amount.Amount);
        PaidAmount = new Money(amount);
    }

    private bool CanTransitionTo(string targetStatus) => Status switch
    {
        OrderStatus.Draft => targetStatus is OrderStatus.Confirmed or OrderStatus.Cancelled,
        OrderStatus.Confirmed => targetStatus is OrderStatus.Preparing or OrderStatus.Cancelled,
        OrderStatus.Preparing => targetStatus is OrderStatus.Ready or OrderStatus.Cancelled,
        OrderStatus.Ready => targetStatus is OrderStatus.Completed or OrderStatus.Cancelled,
        _ => false,
    };
}
