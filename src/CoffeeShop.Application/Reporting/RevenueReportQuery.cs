namespace CoffeeShop.Application.Reporting;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Sales;

public sealed record RevenueReportQuery(
    DateOnly FirstDate,
    DateOnly LastDateInclusive,
    TimeZoneInfo BusinessTimeZone);

public sealed record DailyRevenueBreakdown(
    DateOnly Date,
    decimal Revenue,
    int OrderCount);

public sealed record PaymentMethodRevenueBreakdown(
    string PaymentMethod,
    decimal Revenue,
    int OrderCount);

/// <summary>
/// Revenue and distinct recognized-order counts for one business-date range.
/// A split-payment order is counted once in the overall total and once in each daily or payment-method
/// bucket where it has recognized revenue, so bucket counts are intentionally not additive.
/// </summary>
public sealed record RevenueReport(
    DateOnly FirstDate,
    DateOnly LastDateInclusive,
    string TimeZoneId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtcExclusive,
    decimal Revenue,
    int OrderCount,
    IReadOnlyList<DailyRevenueBreakdown> Daily,
    IReadOnlyList<PaymentMethodRevenueBreakdown> ByPaymentMethod);

public sealed record RevenueOrderCandidate(Order Order, bool InventoryWasRestored);

public interface IRevenueReportRepository
{
    /// <summary>
    /// Loads candidate orders with their line snapshots and complete payment history, including orders
    /// completed in the period even when all payments precede it. Payments before the requested period
    /// are required because the authoritative rule allocates the discounted payable cap chronologically.
    /// </summary>
    Task<IReadOnlyList<RevenueOrderCandidate>> LoadCandidatesAsync(
        RevenuePeriod period,
        CancellationToken cancellationToken = default);
}

public sealed class RevenueReportQueryHandler
{
    private readonly IRevenueReportRepository repository;

    public RevenueReportQueryHandler(IRevenueReportRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Result<RevenueReport>> GetRevenueAsync(
        RevenueReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var periodResult = ReportPeriodValidation.Create(
            query.FirstDate, query.LastDateInclusive, query.BusinessTimeZone);
        if (periodResult.IsFailure)
        {
            return Result<RevenueReport>.Failure(periodResult.Error!);
        }

        var period = periodResult.Value;
        var candidates = await repository.LoadCandidatesAsync(period, cancellationToken);
        var payments = candidates
            .SelectMany(candidate => RevenueRecognitionRule.Recognize(
                candidate.Order, period, candidate.InventoryWasRestored))
            .ToArray();

        var daily = payments
            .GroupBy(payment => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(payment.PaidAt.Value, query.BusinessTimeZone).Date))
            .OrderBy(group => group.Key)
            .Select(group => new DailyRevenueBreakdown(
                group.Key,
                group.Sum(payment => payment.Amount.Amount),
                group.Select(payment => payment.OrderId).Distinct().Count()))
            .ToArray();
        var byPaymentMethod = payments
            .GroupBy(payment => payment.PaymentMethod, StringComparer.Ordinal)
            .OrderBy(group => PaymentMethodOrder(group.Key))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new PaymentMethodRevenueBreakdown(
                group.Key,
                group.Sum(payment => payment.Amount.Amount),
                group.Select(payment => payment.OrderId).Distinct().Count()))
            .ToArray();

        return Result<RevenueReport>.Success(new(
            query.FirstDate,
            query.LastDateInclusive,
            query.BusinessTimeZone.Id,
            period.StartUtc,
            period.EndUtcExclusive,
            payments.Sum(payment => payment.Amount.Amount),
            payments.Select(payment => payment.OrderId).Distinct().Count(),
            daily,
            byPaymentMethod));
    }

    private static int PaymentMethodOrder(string paymentMethod) => paymentMethod switch
    {
        PaymentMethod.Cash => 0,
        PaymentMethod.Card => 1,
        PaymentMethod.EWallet => 2,
        _ => 3,
    };
}
