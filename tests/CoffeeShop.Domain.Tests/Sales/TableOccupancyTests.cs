namespace CoffeeShop.Domain.Tests.Sales;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public class TableOccupancyTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void OccupyAndRelease_TracksTheActiveOrder()
    {
        var table = new DiningTable(EntityId.New(), "T1", 4);
        var orderId = EntityId.New();

        table.Occupy(orderId, 2);
        Assert.True(table.IsOccupied);
        Assert.Equal(orderId, table.ActiveOrderId);

        table.Release(orderId);
        Assert.False(table.IsOccupied);
        Assert.Null(table.ActiveOrderId);
    }

    [Fact]
    public void Occupy_RejectsAnAlreadyOccupiedOrOversizedAssignment()
    {
        var table = new DiningTable(EntityId.New(), "T1", 4);
        var firstOrder = EntityId.New();
        table.Occupy(firstOrder, 4);

        Assert.Throws<InvalidOperationException>(() => table.Occupy(EntityId.New(), 1));
        Assert.Equal(firstOrder, table.ActiveOrderId);

        var smallTable = new DiningTable(EntityId.New(), "T2", 2);
        Assert.Throws<InvalidOperationException>(() => smallTable.Occupy(EntityId.New(), 3));
        Assert.False(smallTable.IsOccupied);
    }

    [Fact]
    public void ReservationRules_RejectOccupiedTablesCapacityAndDuplicateSlots()
    {
        var table = new DiningTable(EntityId.New(), "T1", 2);
        var reservation = CreateReservation(table.Id, 2);
        TableOccupancyRules.ValidateReservation(table, reservation);

        table.Occupy(EntityId.New(), 2);
        Assert.Throws<InvalidOperationException>(() => TableOccupancyRules.ValidateReservation(table, reservation));

        var availableTable = new DiningTable(EntityId.New(), "T2", 2);
        var oversized = CreateReservation(availableTable.Id, 3);
        Assert.Throws<InvalidOperationException>(() => TableOccupancyRules.ValidateReservation(availableTable, oversized));

        var existing = CreateReservation(availableTable.Id, 2);
        var duplicate = CreateReservation(availableTable.Id, 1);
        Assert.Throws<InvalidOperationException>(() => TableOccupancyRules.ValidateReservationSlot(duplicate, [existing]));
    }

    private static Reservation CreateReservation(EntityId tableId, int partySize) =>
        new(EntityId.New(), tableId, "Guest", "0123456789", partySize, Now, EntityId.New(), Now);
}
