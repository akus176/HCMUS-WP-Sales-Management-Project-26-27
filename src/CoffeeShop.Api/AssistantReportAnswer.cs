namespace CoffeeShop.Api;

using CoffeeShop.Contracts;

internal static class AssistantReportAnswer
{
    public static string ExpansionPlan(IReadOnlyList<AssistantToolResult> reports, DateOnly? from, DateOnly? to)
    {
        var metrics = reports.Select(report => report.Metrics).ToArray();
        var revenue = metrics.OfType<RevenueReportResponse>().SingleOrDefault();
        var candidates = metrics.OfType<BestSellerReportResponse>().SingleOrDefault()?.Items
            .Where(item => item.Quantity > 0).Take(2).ToArray() ?? [];
        var channel = metrics.OfType<ChannelReportResponse>().SingleOrDefault()?.ByChannel
            .Where(row => row.OrderCount > 0).OrderByDescending(row => row.Revenue)
            .ThenByDescending(row => row.OrderCount).FirstOrDefault();
        var stock = metrics.OfType<LowStockReportRowResponse[]>().SingleOrDefault();
        var period = from is { } start && to is { } end ? Period(start, end) : "";

        var baseline = revenue switch
        {
            { OrderCount: > 0 } => $"{period}{revenue.Revenue:N2} in recognized revenue from {OrderCount(revenue.OrderCount)}, averaging {revenue.Revenue / revenue.OrderCount:N2} per order. Use a comparable trading week as your starting point.",
            { OrderCount: 0 } => $"{period}no recognized sales in the selected dates. Use Change dates to find a recent period with sales, or track a full trading week before choosing what to expand.",
            _ => "Use a recent trading week to record recognized revenue, orders with recognized payments, and average order value.",
        };
        var pilot = channel is null
            ? "Pick one existing sales channel after comparing its orders, revenue, and cost per order."
            : $"Consider starting with {Label(channel.Channel)}, which recorded {channel.Revenue:N2} in revenue from {OrderCount(channel.OrderCount)} in the selected period. Check its cost per order first.";
        pilot += candidates.Length == 0 ? " Pick one product with consistent demand and test one change, such as giving it more visibility."
            : $" Test one change, such as featuring {string.Join(" or ", candidates.Select(item => $"{item.ItemName} ({item.Quantity:N0} sold overall)"))}.";
        var supply = stock is { Length: > 0 }
            ? $"Current low stock includes {string.Join(", ", stock.Take(3).Select(item => item.Name))}. Check whether the pilot recipes use any of them and confirm supplier lead times before increasing volume."
            : "Check the pilot recipes against available stock, supplier lead times, and staffing capacity before increasing volume.";

        return string.Join("\n\n",
            "Four-week café expansion plan",
            "1. Week 1 — Establish a baseline\n" + baseline,
            "2. Week 2 — Run one small pilot\n" + pilot,
            "3. Weeks 2–3 — Check supply and staffing\n" + supply,
            "4. Week 4 — Decide whether to expand\nCompare the pilot with a similar trading week. Track order count, revenue per order, contribution after direct costs, and stockouts. Include ingredients, labor, and channel fees in the cost calculation. Extend the pilot if the gains hold and the team can handle the volume.",
            "Use actual costs and capacity to assess profitability before making a larger commitment.");
    }

    public static string Describe(AssistantToolResult current, AssistantToolResult? previous, string? view) =>
        current.Metrics switch
        {
            RevenueReportResponse revenue => Revenue(revenue, previous?.Metrics as RevenueReportResponse, view),
            BestSellerReportResponse sellers => BestSellers(sellers, previous?.Metrics as BestSellerReportResponse, view == "variants"),
            ChannelReportResponse channels => Channels(channels, previous?.Metrics as ChannelReportResponse),
            LowStockReportRowResponse[] stock => LowStock(stock),
            _ => "No matching report data was found.",
        };

    private static string Revenue(RevenueReportResponse report, RevenueReportResponse? previous, string? view)
    {
        var lines = new List<string>
        {
            Period(report.FirstDate, report.LastDateInclusive) + (report.OrderCount == 0
                ? "No matching revenue data was found."
                : $"recognized revenue is {report.Revenue:N2} from {report.OrderCount:N0} orders, averaging {report.Revenue / report.OrderCount:N2} per order."),
        };
        if (previous is not null)
        {
            var period = $"Previous period ({previous.FirstDate:yyyy-MM-dd} to {previous.LastDateInclusive:yyyy-MM-dd})";
            if (previous.OrderCount == 0)
                lines.Add($"{period} had no recognized revenue data.");
            else
                lines.Add($"{period}: {previous.Revenue:N2} from {previous.OrderCount:N0} orders, averaging {previous.Revenue / previous.OrderCount:N2} per order.");

            if (report.OrderCount > 0 && previous.OrderCount > 0)
            {
                var change = report.Revenue - previous.Revenue;
                var percentage = previous.Revenue == 0 ? "" : $" ({change / previous.Revenue:P1})";
                var orders = report.OrderCount - previous.OrderCount;
                var average = report.Revenue / report.OrderCount - previous.Revenue / previous.OrderCount;
                lines.Add($"Change from previous period: revenue {Signed(change)}{percentage}, orders {orders:+0;-0;0}, average per order {Signed(average)}.");
            }
        }

        if (view is "daily" or "daily_and_payments")
        {
            if (report.Daily.OrderByDescending(day => day.Revenue).ThenBy(day => day.Date).FirstOrDefault() is { } strongest)
            {
                var leaders = report.Daily.Where(day => day.Revenue == strongest.Revenue).OrderBy(day => day.Date).ToArray();
                var dates = string.Join(", ", leaders.Take(3).Select(day => day.Date.ToString("yyyy-MM-dd")));
                if (leaders.Length > 3) dates += $" and {leaders.Length - 3} more";
                lines.Add($"Highest daily revenue: {strongest.Revenue:N2} on {dates}.");
            }
        }
        if ((view is "payments" or "daily_and_payments") && report.ByPaymentMethod.Count > 0)
        {
            lines.Add("Revenue by payment method: " + string.Join("; ", report.ByPaymentMethod
                .OrderByDescending(row => row.Revenue)
                .Select(row => $"{Label(row.PaymentMethod)} {row.Revenue:N2}{Share(row.Revenue, report.Revenue)}")) + ".");
        }
        return string.Join('\n', lines);
    }

    private static string BestSellers(BestSellerReportResponse report, BestSellerReportResponse? previous, bool variants)
    {
        IReadOnlyList<BestSellerRowResponse> Rows(BestSellerReportResponse value) => variants ? value.Variants : value.Items;
        var rows = Rows(report);
        var group = variants ? "Sizes" : "Products";
        var basis = report.SortBy == "revenue" ? "recognized revenue" : "units sold";
        var result = Period(report.FirstDate, report.LastDateInclusive) + (rows.Count == 0
            ? "No matching best-seller data was found."
            : $"{group} ranked by {basis} across all channels\n" + FormatRanking(rows));
        if (previous is null) return result;

        var priorRows = Rows(previous);
        return result + $"\nPrevious period ({previous.FirstDate:yyyy-MM-dd} to {previous.LastDateInclusive:yyyy-MM-dd}): " +
            (priorRows.Count == 0 ? "No matching best-seller data was found." : $"\n{FormatRanking(priorRows)}");

        string FormatRanking(IReadOnlyList<BestSellerRowResponse> ranking) => string.Join('\n', ranking.Select(row =>
            $"{row.Rank}. {row.ItemName}{(variants && row.SizeName is not null ? $" ({row.SizeName})" : "")} · {row.Quantity:N0} sold · {row.Revenue:N2} revenue"));
    }

    private static string Channels(ChannelReportResponse report, ChannelReportResponse? previous)
    {
        var result = DescribePeriod(report);
        if (previous is null) return result;

        result += "\nPrevious period\n" + DescribePeriod(previous);
        if (report.OrderCount == 0 || previous.OrderCount == 0) return result;

        var changes = report.ByChannel.Where(row => previous.ByChannel.Any(prior => prior.Channel == row.Channel)).Select(row =>
        {
            var prior = previous.ByChannel.First(candidate => candidate.Channel == row.Channel);
            return $"{Label(row.Channel)} {Signed(row.Revenue - prior.Revenue)} revenue";
        });
        return result + "\nChange from previous period: " + string.Join("; ", changes) + ".";

        static string DescribePeriod(ChannelReportResponse report) => Period(report.FirstDate, report.LastDateInclusive)
            + (report.OrderCount == 0 ? "No matching order-channel data was found." : "Revenue and orders by channel\n"
                + string.Join('\n', report.ByChannel.OrderByDescending(row => row.Revenue)
                    .Select(row => $"• {Label(row.Channel)} · {row.Revenue:N2}{Share(row.Revenue, report.Revenue)} revenue · {row.OrderCount:N0}{Share(row.OrderCount, report.OrderCount)} orders")));
    }

    private static string LowStock(LowStockReportRowResponse[] stock)
    {
        if (stock.Length == 0) return "Current stock: no active ingredients are at or below their reorder threshold.";
        var rows = stock.Take(10).Select(row =>
        {
            var shortfall = row.ReorderThreshold - row.AvailableQuantity;
            var shortage = shortfall > 0 ? $"{shortfall:N3} {row.BaseUnit} below threshold" : "at threshold";
            return $"• {row.Name} · {row.AvailableQuantity:N3} {row.BaseUnit} on hand · {shortage}";
        });
        return $"Current low stock ({stock.Length:N0} ingredients)\n" + string.Join('\n', rows)
            + (stock.Length > 10 ? $"\nSee Stock room for the other {stock.Length - 10:N0}." : "");
    }

    private static string Period(DateOnly from, DateOnly to) => $"From {from:yyyy-MM-dd} through {to:yyyy-MM-dd}: ";

    private static string OrderCount(int count) => count == 1 ? "1 order" : $"{count:N0} orders";

    private static string Signed(decimal amount) => amount.ToString("+#,##0.00;-#,##0.00;0.00");

    private static string Share(decimal value, decimal total) => total > 0 ? $" ({value / total:P1})" : "";

    private static string Label(string value) => value switch
    {
        "dine_in" => "Dine-in",
        "takeaway" => "Takeaway",
        "delivery" => "Delivery",
        "cash" => "Cash",
        "card" => "Card",
        "e_wallet" => "E-wallet",
        _ => value.Replace('_', ' '),
    };
}
