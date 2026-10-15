namespace CoffeeShop.Application.Reporting;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

/// <summary>
/// The single authoritative rule for revenue queries.
/// Revenue is recognized at each successful payment's <see cref="Payment.PaidAt"/> instant only when
/// the order is fully paid and completed. Cancelled orders, orders with a cancellation timestamp, and
/// orders whose consumed inventory was restored recognize no revenue. Recognized payments are capped,
/// in payment-time order, at <see cref="Order.Total"/>: the final payable amount after approved ordinary
/// and loyalty discounts. Pending or rejected discounts therefore do not change revenue, and overpayment
/// is never revenue.
/// </summary>
public static class RevenueRecognitionRule
{
    /// <summary>
    /// Returns the recognized portions of successful payments whose payment instants fall in
    /// <paramref name="period"/>. All revenue queries must use this method rather than restating the rule.
    /// </summary>
    public static IReadOnlyList<RecognizedRevenuePayment> Recognize(
        Order order,
        RevenuePeriod period,
        bool inventoryWasRestored = false)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(period);

        if (order.Status != OrderStatus.Completed
            || order.CompletedAt is null
            || order.CancelledAt is not null
            || inventoryWasRestored
            || order.Total.Amount == 0)
        {
            return [];
        }

        var completionInstant = order.CompletedAt.Value;
        var successfulPayments = order.Payments
            .Where(payment => payment.Status == PaymentStatus.Succeeded
                && payment.PaidAt.Value <= completionInstant)
            .OrderBy(payment => payment.PaidAt.Value)
            .ThenBy(payment => payment.Id.Value)
            .ToArray();

        if (successfulPayments.Sum(payment => payment.Amount.Amount) < order.Total.Amount)
        {
            return [];
        }

        var remainingPayable = order.Total.Amount;
        var recognized = new List<RecognizedRevenuePayment>();
        foreach (var payment in successfulPayments)
        {
            var recognizedAmount = decimal.Min(payment.Amount.Amount, remainingPayable);
            remainingPayable -= recognizedAmount;
            if (recognizedAmount > 0 && period.Contains(payment.PaidAt))
            {
                recognized.Add(new(
                    order.Id,
                    payment.Id,
                    payment.Method,
                    new Money(recognizedAmount),
                    payment.PaidAt));
            }

            if (remainingPayable == 0)
            {
                break;
            }
        }

        return recognized;
    }
}

/// <summary>
/// A recognized part of a successful payment. The amount can be less than the recorded payment when
/// the payment would make total successful payments exceed the order's discounted payable total.
/// </summary>
public sealed record RecognizedRevenuePayment(
    EntityId OrderId,
    EntityId PaymentId,
    string PaymentMethod,
    Money Amount,
    UtcTimestamp PaidAt);

/// <summary>
/// An inclusive-start, exclusive-end UTC window derived from inclusive local business dates.
/// Using a half-open interval makes adjacent report periods non-overlapping. Local midnights that do
/// not exist because of a time-zone transition are rejected; an ambiguous midnight uses its earliest
/// UTC occurrence so the selected local date begins deterministically.
/// </summary>
public sealed record RevenuePeriod
{
    private RevenuePeriod(DateTimeOffset startUtc, DateTimeOffset endUtcExclusive)
    {
        StartUtc = startUtc.ToUniversalTime();
        EndUtcExclusive = endUtcExclusive.ToUniversalTime();
        if (StartUtc >= EndUtcExclusive)
        {
            throw new ArgumentException("A revenue period must end after it starts.", nameof(endUtcExclusive));
        }
    }

    public DateTimeOffset StartUtc { get; }

    public DateTimeOffset EndUtcExclusive { get; }

    /// <summary>
    /// Converts an inclusive local date range into a UTC half-open interval in the supplied business time zone.
    /// </summary>
    public static RevenuePeriod FromLocalDates(
        DateOnly firstDate,
        DateOnly lastDateInclusive,
        TimeZoneInfo businessTimeZone)
    {
        ArgumentNullException.ThrowIfNull(businessTimeZone);
        if (lastDateInclusive < firstDate)
        {
            throw new ArgumentOutOfRangeException(nameof(lastDateInclusive),
                "The final revenue date cannot precede the first date.");
        }

        var endDateExclusive = lastDateInclusive.AddDays(1);
        return new(
            ConvertBoundaryToUtc(firstDate, businessTimeZone),
            ConvertBoundaryToUtc(endDateExclusive, businessTimeZone));
    }

    public bool Contains(UtcTimestamp instant)
    {
        ArgumentNullException.ThrowIfNull(instant);
        return instant.Value >= StartUtc && instant.Value < EndUtcExclusive;
    }

    private static DateTimeOffset ConvertBoundaryToUtc(DateOnly date, TimeZoneInfo timeZone)
    {
        var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(localMidnight))
        {
            throw new ArgumentException(
                $"Local midnight on {date:yyyy-MM-dd} does not exist in time zone '{timeZone.Id}'.",
                nameof(date));
        }

        var offset = timeZone.IsAmbiguousTime(localMidnight)
            ? timeZone.GetAmbiguousTimeOffsets(localMidnight).Max()
            : timeZone.GetUtcOffset(localMidnight);
        return new DateTimeOffset(localMidnight, offset).ToUniversalTime();
    }
}
