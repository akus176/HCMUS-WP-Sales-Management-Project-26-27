namespace CoffeeShop.Application.Reporting;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public enum BestSellerSort
{
    Quantity,
    Revenue,
}

public sealed record BestSellerReportQuery(
    DateOnly FirstDate,
    DateOnly LastDateInclusive,
    TimeZoneInfo BusinessTimeZone,
    BestSellerSort SortBy = BestSellerSort.Quantity,
    int Limit = 10);

public sealed record BestSellerRow(
    int Rank,
    EntityId? MenuItemId,
    EntityId? SizeVariantId,
    string ItemName,
    string? SizeName,
    long Quantity,
    decimal Revenue);

/// <summary>
/// Quantity uses completion dates; revenue uses the shared payment-date rule. Each list is limited
/// independently. Names come from the latest included order snapshot, never the current catalog.
/// </summary>
public sealed record BestSellerReport(
    DateOnly FirstDate,
    DateOnly LastDateInclusive,
    string TimeZoneId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtcExclusive,
    BestSellerSort SortBy,
    IReadOnlyList<BestSellerRow> Items,
    IReadOnlyList<BestSellerRow> Variants);

public sealed class BestSellerReportQueryHandler(IRevenueReportRepository repository)
{
    public async Task<Result<BestSellerReport>> GetBestSellersAsync(
        BestSellerReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var periodResult = ReportPeriodValidation.Create(
            query.FirstDate, query.LastDateInclusive, query.BusinessTimeZone);
        if (periodResult.IsFailure)
        {
            return Result<BestSellerReport>.Failure(periodResult.Error!);
        }

        if (!Enum.IsDefined(query.SortBy) || query.Limit <= 0)
        {
            return Result<BestSellerReport>.Failure(Error.Validation(
                "reports.invalid_ranking", "A supported ranking and a positive limit are required."));
        }

        var period = periodResult.Value;
        var candidates = await repository.LoadCandidatesAsync(period, cancellationToken);
        var sales = new List<LineSale>();
        foreach (var candidate in candidates)
        {
            var order = candidate.Order;
            if (!RevenueRecognitionRule.IsEligible(order, candidate.InventoryWasRestored))
            {
                continue;
            }

            var lines = order.Lines.OrderBy(line => line.Id.Value).ToArray();
            var lineRevenue = new decimal[lines.Length];
            var subtotal = lines.Sum(line => line.LineTotal.Amount);
            foreach (var payment in RevenueRecognitionRule.Recognize(order, period, candidate.InventoryWasRestored))
            {
                // Allocate each recognized payment separately so adjacent date ranges remain additive.
                // Largest remainders receive spare cents; line identity breaks equal-remainder ties.
                var shares = lines.Select(line => payment.Amount.Amount * line.LineTotal.Amount / subtotal).ToArray();
                var rounded = shares.Select(share => decimal.Floor(share * 100m) / 100m).ToArray();
                var spareCents = (int)((payment.Amount.Amount - rounded.Sum()) * 100m);
                foreach (var index in Enumerable.Range(0, lines.Length)
                    .OrderByDescending(index => shares[index] - rounded[index]).ThenBy(index => lines[index].Id.Value)
                    .Take(spareCents))
                {
                    rounded[index] += 0.01m;
                }

                for (var index = 0; index < lines.Length; index++)
                {
                    lineRevenue[index] += rounded[index];
                }
            }

            for (var index = 0; index < lines.Length; index++)
            {
                var quantity = period.Contains(order.CompletedAt!) ? lines[index].Quantity : 0;
                if (quantity > 0 || lineRevenue[index] > 0)
                {
                    sales.Add(new(lines[index], order.Id, order.CompletedAt!, quantity, lineRevenue[index]));
                }
            }
        }

        return Result<BestSellerReport>.Success(new(
            query.FirstDate, query.LastDateInclusive, query.BusinessTimeZone.Id,
            period.StartUtc, period.EndUtcExclusive, query.SortBy,
            Rank(sales, query, byVariant: false), Rank(sales, query, byVariant: true)));
    }

    private static BestSellerRow[] Rank(List<LineSale> sales, BestSellerReportQuery query, bool byVariant)
    {
        var rows = sales.GroupBy(sale => new
        {
            sale.Line.MenuItemId,
            SizeVariantId = byVariant ? sale.Line.SizeVariantId : null,
            ItemName = sale.Line.MenuItemId is null ? sale.Line.ItemNameSnapshot : string.Empty,
            SizeName = byVariant && sale.Line.SizeVariantId is null ? sale.Line.SizeNameSnapshot : string.Empty,
        }).Select(group =>
        {
            var latest = group.OrderByDescending(sale => sale.CompletedAt.Value)
                .ThenBy(sale => sale.OrderId.Value).ThenBy(sale => sale.Line.Id.Value).First().Line;
            return new BestSellerRow(0, group.Key.MenuItemId, group.Key.SizeVariantId,
                latest.ItemNameSnapshot, byVariant ? latest.SizeNameSnapshot : null,
                group.Sum(sale => sale.Quantity), group.Sum(sale => sale.Revenue));
        });

        return rows.OrderByDescending(row => query.SortBy == BestSellerSort.Quantity ? row.Quantity : row.Revenue)
            .ThenByDescending(row => query.SortBy == BestSellerSort.Quantity ? row.Revenue : row.Quantity)
            .ThenBy(row => row.ItemName, StringComparer.Ordinal).ThenBy(row => row.SizeName, StringComparer.Ordinal)
            .ThenBy(row => row.MenuItemId?.Value).ThenBy(row => row.SizeVariantId?.Value)
            .Take(query.Limit).Select((row, index) => row with { Rank = index + 1 }).ToArray();
    }

    private sealed record LineSale(OrderLine Line, EntityId OrderId, UtcTimestamp CompletedAt, long Quantity, decimal Revenue);
}
