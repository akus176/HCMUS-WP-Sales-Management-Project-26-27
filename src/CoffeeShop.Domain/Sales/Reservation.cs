namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class Reservation
{
    protected Reservation()
    {
    }

    public Reservation(
        EntityId id,
        EntityId tableId,
        string guestName,
        string contactNumber,
        int partySize,
        UtcTimestamp reservedFor,
        EntityId createdBy,
        UtcTimestamp createdAt,
        EntityId? customerId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(tableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(guestName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactNumber);
        ArgumentNullException.ThrowIfNull(reservedFor);
        ArgumentNullException.ThrowIfNull(createdBy);
        ArgumentNullException.ThrowIfNull(createdAt);
        if (partySize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(partySize));
        }

        Id = id;
        TableId = tableId;
        GuestName = guestName.Trim();
        ContactNumber = contactNumber.Trim();
        PartySize = partySize;
        ReservedFor = reservedFor;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        CustomerId = customerId;
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

    public void Cancel(UtcTimestamp cancelledAt)
    {
        ArgumentNullException.ThrowIfNull(cancelledAt);
        if (Status is "cancelled" or "completed")
        {
            throw new InvalidOperationException("Only an active reservation can be cancelled.");
        }

        Status = "cancelled";
        CancelledAt = cancelledAt;
    }
}
