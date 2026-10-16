namespace CoffeeShop.Application.Reporting;

using CoffeeShop.Application.Common.Results;

public sealed record OrderChannelReportQuery(
    DateOnly FirstDate,
    DateOnly LastDateInclusive,
    TimeZoneInfo BusinessTimeZone);

public sealed record OrderChannelRevenueBreakdown(
    string Channel,
    decimal Revenue,
    int OrderCount);

/// <summary>
/// Recognized revenue and distinct recognized-order counts by channel for one business-date range.
/// Every supported channel is returned, including zero-valued channels. Each order belongs to one
/// channel, so both channel revenues and counts add up to the report totals.
/// </summary>
public sealed record OrderChannelReport(
    DateOnly FirstDate,
    DateOnly LastDateInclusive,
    string TimeZoneId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtcExclusive,
    decimal Revenue,
    int OrderCount,
    IReadOnlyList<OrderChannelRevenueBreakdown> ByChannel);

public sealed class OrderChannelReportQueryHandler
{
    private readonly IRevenueReportRepository repository;

    public OrderChannelReportQueryHandler(IRevenueReportRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Result<OrderChannelReport>> GetChannelsAsync(
        OrderChannelReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var periodResult = ReportPeriodValidation.Create(
            query.FirstDate, query.LastDateInclusive, query.BusinessTimeZone);
        if (periodResult.IsFailure)
        {
            return Result<OrderChannelReport>.Failure(periodResult.Error!);
        }

        var period = periodResult.Value;
        var candidates = await repository.LoadCandidatesAsync(period, cancellationToken);
        // Recognize complete payment histories before grouping: earlier payments may consume the
        // payable cap, and multiple in-period payments must still count as one order in its channel.
        var recognizedOrders = candidates
            .Select(candidate => new
            {
                candidate.Order.Id,
                candidate.Order.Channel,
                Revenue = RevenueRecognitionRule.Recognize(
                    candidate.Order, period, candidate.InventoryWasRestored)
                    .Sum(payment => payment.Amount.Amount),
            })
            .Where(order => order.Revenue > 0)
            .ToArray();
        var channelTotals = recognizedOrders
            .GroupBy(order => order.Channel, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => new OrderChannelRevenueBreakdown(
                group.Key,
                group.Sum(order => order.Revenue),
                group.Select(order => order.Id).Distinct().Count()), StringComparer.Ordinal);
        string[] channels = ["dine_in", "takeaway", "delivery"];
        var byChannel = channels
            .Select(channel => channelTotals.GetValueOrDefault(channel)
                ?? new OrderChannelRevenueBreakdown(channel, 0m, 0))
            .ToArray();

        return Result<OrderChannelReport>.Success(new(
            query.FirstDate,
            query.LastDateInclusive,
            query.BusinessTimeZone.Id,
            period.StartUtc,
            period.EndUtcExclusive,
            recognizedOrders.Sum(order => order.Revenue),
            recognizedOrders.Select(order => order.Id).Distinct().Count(),
            byChannel));
    }
}
