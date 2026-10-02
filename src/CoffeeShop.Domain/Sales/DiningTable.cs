namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class DiningTable
{
    protected DiningTable()
    {
    }

    public DiningTable(EntityId id, string tableNumber, int? seats = null, bool isOccupied = false, EntityId? activeOrderId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableNumber);
        if (seats is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seats));
        }

        Id = id;
        TableNumber = tableNumber.Trim();
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
}
