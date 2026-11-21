using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;

namespace CoffeeShop.WinUI.ViewModels;

/// <summary>
/// Presentation-only projection and filter helpers shared by the reports page.
/// Report calculations and date-boundary rules remain in the Application layer.
/// </summary>
public static class ReportPresentation
{
    public static bool TryCreatePeriod(
        DateTimeOffset firstDate,
        DateTimeOffset lastDate,
        out DateOnly first,
        out DateOnly last,
        out string? validationMessage)
    {
        first = DateOnly.FromDateTime(firstDate.DateTime);
        last = DateOnly.FromDateTime(lastDate.DateTime);
        validationMessage = null;

        if (firstDate == default || lastDate == default)
        {
            validationMessage = "Choose both the first and last day for the reporting period.";
            return false;
        }

        if (last < first)
        {
            validationMessage = "The last day must be on or after the first day.";
            return false;
        }

        return true;
    }

    public static string PeriodLabel(DateOnly first, DateOnly last) =>
        $"Reporting period: {first:dd MMM yyyy} – {last:dd MMM yyyy}";

    public static IReadOnlyList<RevenuePaymentRow> RevenuePayments(RevenueReport report, string? paymentMethod) =>
        report.ByPaymentMethod
            .Where(row => string.IsNullOrWhiteSpace(paymentMethod)
                || string.Equals(row.PaymentMethod, paymentMethod, StringComparison.Ordinal))
            .Select(row => new RevenuePaymentRow(row.PaymentMethod, row.Revenue, row.OrderCount))
            .ToArray();

    public static IReadOnlyList<RevenuePeriodRow> RevenuePeriods(RevenueReport report, RevenueGrouping grouping)
    {
        if (grouping == RevenueGrouping.Day)
            return report.Daily.Select(row => new RevenuePeriodRow(row.Date, row.Revenue, grouping, row.OrderCount)).ToArray();

        // Revenue is additive across payment dates. Distinct order counts are not,
        // because a split payment can recognize the same order on multiple days.
        return report.Daily
            .GroupBy(row => new DateOnly(row.Date.Year, grouping == RevenueGrouping.Month ? row.Date.Month : 1, 1))
            .OrderBy(period => period.Key)
            .Select(period => new RevenuePeriodRow(period.Key, period.Sum(row => row.Revenue), grouping, null))
            .ToArray();
    }

    public static IReadOnlyList<BestSellerViewRow> BestSellers(BestSellerReport report) =>
        report.Items.Select(row => new BestSellerViewRow(
            row.Rank,
            row.ItemName,
            row.SizeName,
            row.Quantity,
            row.Revenue)).ToArray();

    public static IReadOnlyList<OrderChannelViewRow> Channels(OrderChannelReport report, string? channel) =>
        report.ByChannel
            .Where(row => string.IsNullOrWhiteSpace(channel)
                || string.Equals(row.Channel, channel, StringComparison.Ordinal))
            .Select(row => new OrderChannelViewRow(row.Channel, row.Revenue, row.OrderCount))
            .ToArray();

    public static IReadOnlyList<LowStockViewRow> LowStock(IEnumerable<LowStockIngredient> ingredients) =>
        ingredients.Select(row => new LowStockViewRow(
            row.Name,
            row.BaseUnit,
            row.AvailableQuantity,
            row.ReorderThreshold)).ToArray();
}

public sealed record RevenuePaymentRow(string PaymentMethod, decimal Revenue, int OrderCount)
{
    public string RevenueDisplay => Revenue.ToString("0.00");
}

public enum RevenueGrouping { Day, Month, Year }

public sealed record RevenuePeriodRow(DateOnly Date, decimal Revenue, RevenueGrouping Grouping, int? OrderCount)
{
    public string ChartLabel => Grouping switch
    {
        RevenueGrouping.Month => Date.ToString("MMM yyyy"),
        RevenueGrouping.Year => Date.ToString("yyyy"),
        _ => Date.ToString("dd MMM"),
    };
    public string PeriodLabel => Grouping == RevenueGrouping.Day ? Date.ToString("dd MMM yyyy") : ChartLabel;
    public string TableLabel => OrderCount is { } count
        ? $"{PeriodLabel} · {count:N0} {(count == 1 ? "order" : "orders")}"
        : PeriodLabel;
    public string RevenueDisplay => Revenue.ToString("N2");
}

public sealed record BestSellerViewRow(int Rank, string ItemName, string? SizeName, long Quantity, decimal Revenue)
{
    public string NameDisplay => string.IsNullOrWhiteSpace(SizeName) ? ItemName : $"{ItemName} · {SizeName}";
    public string RevenueDisplay => Revenue.ToString("0.00");
}

public sealed record OrderChannelViewRow(string Channel, decimal Revenue, int OrderCount)
{
    public string ChannelDisplay => Channel switch
    {
        "dine_in" => "Dine-in",
        "takeaway" => "Takeaway",
        "delivery" => "Delivery",
        _ => Channel,
    };

    public string RevenueDisplay => Revenue.ToString("0.00");
}

public sealed record LowStockViewRow(string Name, string BaseUnit, decimal AvailableQuantity, decimal ReorderThreshold)
{
    public string BalanceDisplay => $"{AvailableQuantity:0.###} {BaseUnit}";
    public string ThresholdDisplay => $"{ReorderThreshold:0.###} {BaseUnit}";
}
