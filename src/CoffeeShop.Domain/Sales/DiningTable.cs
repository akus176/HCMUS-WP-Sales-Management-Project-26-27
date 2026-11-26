namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class DiningTable
{
    protected DiningTable()
    {
    }

    public DiningTable(EntityId id, string tableNumber, int? seats = null, bool isOccupied = false,
        EntityId? activeOrderId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableNumber);
        if (seats is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seats));
        }

        Id = id;
        TableNumber = ValidateNumber(tableNumber);
        Seats = seats;
        IsOccupied = isOccupied;
        ActiveOrderId = activeOrderId;
    }

    public EntityId Id { get; protected set; } = null!;
    public string TableNumber { get; protected set; } = string.Empty;
    public int? Seats { get; protected set; }
    public bool IsOccupied { get; protected set; }
    public EntityId? ActiveOrderId { get; protected set; }
    public uint Version { get; protected set; }

    public bool CanSeat(int partySize)
    {
        if (partySize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(partySize));
        }

        return !IsOccupied && (Seats is null || partySize <= Seats.Value);
    }

    public void UpdateDetails(string tableNumber, int? seats)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableNumber);
        if (seats is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seats));
        }

        TableNumber = ValidateNumber(tableNumber);
        Seats = seats;
    }

    public static string ValidateNumber(string tableNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableNumber);
        var trimmed = tableNumber.Trim();
        if (trimmed.Length > 20)
        {
            throw new ArgumentException("Table number must be 20 characters or fewer.", nameof(tableNumber));
        }

        return trimmed;
    }

    /// <summary>
    /// Assigns one active dine-in order. The application must execute this mutation
    /// in a transaction after locking this row; <see cref="Version"/> lets EF detect
    /// a concurrent update that acquired the row first.
    /// </summary>
    public void Occupy(EntityId orderId, int partySize)
    {
        ArgumentNullException.ThrowIfNull(orderId);
        if (!CanSeat(partySize))
        {
            throw new InvalidOperationException($"Table '{TableNumber}' is unavailable for this order.");
        }

        IsOccupied = true;
        ActiveOrderId = orderId;
    }

    public void Release(EntityId orderId)
    {
        ArgumentNullException.ThrowIfNull(orderId);
        if (!IsOccupied || ActiveOrderId != orderId)
        {
            throw new InvalidOperationException($"Table '{TableNumber}' is not occupied by this order.");
        }

        IsOccupied = false;
        ActiveOrderId = null;
    }
}
