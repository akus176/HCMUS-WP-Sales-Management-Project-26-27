namespace CoffeeShop.Domain.Sales;

public static class TableOccupancyRules
{
    public static void ValidateReservation(DiningTable table, Reservation reservation)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(reservation);

        if (table.IsOccupied)
        {
            throw new InvalidOperationException($"Table '{table.TableNumber}' is occupied.");
        }

        if (table.Seats is not null && reservation.PartySize > table.Seats.Value)
        {
            throw new InvalidOperationException($"Reservation exceeds table '{table.TableNumber}' capacity.");
        }
    }

    public static void ValidateReservationSlot(Reservation candidate, IEnumerable<Reservation> existingReservations)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(existingReservations);

        if (existingReservations.Any(existing =>
                existing.TableId == candidate.TableId &&
                existing.ReservedFor == candidate.ReservedFor &&
                existing.Status is not ("cancelled" or "completed")))
        {
            throw new InvalidOperationException("The table already has an active reservation for this time.");
        }
    }
}
