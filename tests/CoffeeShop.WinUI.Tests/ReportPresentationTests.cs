using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.ViewModels;

namespace CoffeeShop.WinUI.Tests;

public sealed class ReportPresentationTests
{
    private static readonly DateOnly FirstDate = new(2026, 10, 1);
    private static readonly DateOnly LastDate = new(2026, 10, 7);

    [Fact]
    public void RevenueView_RendersKnownBreakdownsAndAppliesPaymentFilter()
    {
        var report = new RevenueReport(
            FirstDate, LastDate, "UTC", DateTimeOffset.MinValue, DateTimeOffset.MaxValue,
            125.50m, 3,
            [new(new(2026, 10, 1), 75.50m, 2), new(new(2026, 10, 2), 50m, 1)],
            [new("cash", 75.50m, 2), new("card", 50m, 1)]);

        var days = ReportPresentation.RevenueDays(report);
        var cash = ReportPresentation.RevenuePayments(report, "cash");

        Assert.Collection(days,
            row => Assert.Equal("01 Oct", row.DateDisplay),
            row => Assert.Equal("50.00", row.RevenueDisplay));
        var payment = Assert.Single(cash);
        Assert.Equal("cash", payment.PaymentMethod);
        Assert.Equal("75.50", payment.RevenueDisplay);
        Assert.Empty(ReportPresentation.RevenuePayments(report, "e_wallet"));
    }

    [Fact]
    public void BestSellerView_RendersKnownSeededRanking()
    {
        var report = new BestSellerReport(
            FirstDate, LastDate, "UTC", DateTimeOffset.MinValue, DateTimeOffset.MaxValue,
            BestSellerSort.Quantity,
            [new(1, null, null, "Iced latte", null, 12, 420m)],
            []);

        var row = Assert.Single(ReportPresentation.BestSellers(report));

        Assert.Equal(1, row.Rank);
        Assert.Equal("Iced latte", row.NameDisplay);
        Assert.Equal(12, row.Quantity);
        Assert.Equal("420.00", row.RevenueDisplay);
    }

    [Fact]
    public void ChannelAndLowStockViews_RenderKnownSeededDataAndFilterChannel()
    {
        var channels = new OrderChannelReport(
            FirstDate, LastDate, "UTC", DateTimeOffset.MinValue, DateTimeOffset.MaxValue,
            125.50m, 3,
            [new("dine_in", 25.50m, 1), new("takeaway", 100m, 2), new("delivery", 0m, 0)]);
        var ingredients = new[]
        {
            new LowStockIngredient(EntityId.New(), "Whole milk", "ml", 250m, 300m),
        };

        var takeaway = Assert.Single(ReportPresentation.Channels(channels, "takeaway"));
        var lowStock = Assert.Single(ReportPresentation.LowStock(ingredients));

        Assert.Equal("Takeaway", takeaway.ChannelDisplay);
        Assert.Equal("100.00", takeaway.RevenueDisplay);
        Assert.Equal("250 ml", lowStock.BalanceDisplay);
        Assert.Equal("300 ml", lowStock.ThresholdDisplay);
    }

    [Fact]
    public void DateRangeValidation_RejectsMissingAndReversedPeriods()
    {
        var missing = ReportPresentation.TryCreatePeriod(default, DateTimeOffset.Now, out _, out _, out var missingMessage);
        var reversed = ReportPresentation.TryCreatePeriod(
            new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero),
            out _, out _, out var reversedMessage);

        Assert.False(missing);
        Assert.Contains("both", missingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(reversed);
        Assert.Contains("on or after", reversedMessage, StringComparison.OrdinalIgnoreCase);
    }
}
