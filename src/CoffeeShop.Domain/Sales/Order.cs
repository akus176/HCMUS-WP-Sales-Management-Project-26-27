namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class Order
{
    private readonly List<OrderLine> _lines = [];
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
        if (delivery is not null && delivery.OrderId != id)
        {
            throw new ArgumentException("Delivery details must belong to this order.", nameof(delivery));
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
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
}
