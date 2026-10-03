namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class Delivery
{
    protected Delivery()
    {
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId OrderId { get; protected set; } = null!;
    public string RecipientName { get; protected set; } = string.Empty;
    public string ContactNumber { get; protected set; } = string.Empty;
    public string AddressLine { get; protected set; } = string.Empty;
    public EntityId? CustomerAddressId { get; protected set; }
    public string Status { get; protected set; } = "unassigned";
    public EntityId? AssignedStaffId { get; protected set; }
    public UtcTimestamp? AssignedAt { get; protected set; }
    public UtcTimestamp? OutForDeliveryAt { get; protected set; }
    public UtcTimestamp? DeliveredAt { get; protected set; }
    public UtcTimestamp? FailedAt { get; protected set; }
    public string? FailureReason { get; protected set; }
}
