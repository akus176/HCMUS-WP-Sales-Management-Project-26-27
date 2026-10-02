namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class Reservation
{
    protected Reservation()
    {
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId TableId { get; protected set; } = null!;
    public EntityId? CustomerId { get; protected set; }
    public string GuestName { get; protected set; } = string.Empty;
    public string ContactNumber { get; protected set; } = string.Empty;
    public int PartySize { get; protected set; }
    public UtcTimestamp ReservedFor { get; protected set; } = null!;
    public string Status { get; protected set; } = "pending";
    public EntityId CreatedBy { get; protected set; } = null!;
    public UtcTimestamp CreatedAt { get; protected set; } = null!;
    public UtcTimestamp? CancelledAt { get; protected set; }
}
