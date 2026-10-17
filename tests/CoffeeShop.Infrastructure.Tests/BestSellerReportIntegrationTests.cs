namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Reporting;
using Microsoft.Extensions.DependencyInjection;

[Collection(nameof(ReportingDatabaseCollection))]
public sealed class BestSellerReportIntegrationTests(ReportingDatabaseFixture fixture)
{
    private static readonly TimeZoneInfo ShopZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    [PostgresTheory]
    [InlineData(BestSellerSort.Quantity)]
    [InlineData(BestSellerSort.Revenue)]
    public async Task OctoberRankings_UsePersistedSnapshotsAndReconcileWithRevenue(BestSellerSort sort)
    {
        var report = await GetReportAsync(new(2026, 10, 1), new(2026, 10, 31), sort);

        Assert.Equal(sort == BestSellerSort.Quantity
            ? new[] { "Latte", "Tea", "Complimentary", "Espresso" }
            : new[] { "Latte", "Tea", "Espresso", "Complimentary" }, report.Items.Select(row => row.ItemName));
        Assert.Equal(3, report.Items[0].Quantity);
        Assert.Equal(68m, report.Items[0].Revenue);
        Assert.Equal(32m, report.Items.Single(row => row.ItemName == "Tea").Revenue);
        Assert.Equal(0, report.Items.Single(row => row.ItemName == "Espresso").Quantity);
        Assert.Equal(30m, report.Items.Single(row => row.ItemName == "Espresso").Revenue);
        Assert.Equal(fixture.LatteId, report.Items[0].MenuItemId);
        Assert.Equal(sort == BestSellerSort.Quantity ? "Tea" : "Latte", report.Variants[0].ItemName);
        Assert.Equal(sort == BestSellerSort.Quantity ? "Small" : "Large", report.Variants[0].SizeName);
        Assert.Equal(sort == BestSellerSort.Quantity ? null : fixture.LargeId, report.Variants[0].SizeVariantId);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, report.Variants.Select(row => row.Rank));
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 17, 0, 0, TimeSpan.Zero), report.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 17, 0, 0, TimeSpan.Zero), report.EndUtcExclusive);
    }

    [PostgresFact]
    public async Task November_CountsCompletionQuantityWhileRevenueFollowsThePaymentMonth()
    {
        var report = await GetReportAsync(new(2026, 11, 1), new(2026, 11, 30));

        Assert.Equal(new[] { "Espresso", "Latte" }, report.Items.Select(row => row.ItemName));
        Assert.Equal(new long[] { 3, 1 }, report.Items.Select(row => row.Quantity));
        Assert.Equal(new[] { 0m, 40m }, report.Items.Select(row => row.Revenue));
    }

    [PostgresFact]
    public async Task CompletionDay_LoadsPrepaidAndComplimentaryOrdersWithoutInPeriodPayments()
    {
        var prepaid = await GetReportAsync(new(2026, 10, 1), new(2026, 10, 1), timeZone: TimeZoneInfo.Utc);
        Assert.Equal(2, Assert.Single(prepaid.Items).Quantity);
        Assert.Equal(0m, prepaid.Items[0].Revenue);

        var complimentary = await GetReportAsync(new(2026, 10, 25), new(2026, 10, 25));
        Assert.Equal(1, Assert.Single(complimentary.Items).Quantity);
        Assert.Equal(0m, complimentary.Items[0].Revenue);
    }

    [PostgresFact]
    public async Task InvalidOnlyRange_ExcludesCancelledRestoredUnderpaidAndUnsuccessfulOrders()
    {
        var report = await GetReportAsync(new(2026, 10, 20), new(2026, 10, 24));
        Assert.Empty(report.Items);
        Assert.Empty(report.Variants);
    }

    [PostgresFact]
    public async Task EmptyRange_ReturnsNoRankings()
    {
        var report = await GetReportAsync(new(2026, 8, 1), new(2026, 8, 31));
        Assert.Empty(report.Items);
        Assert.Empty(report.Variants);
    }

    private async Task<BestSellerReport> GetReportAsync(DateOnly first, DateOnly last,
        BestSellerSort sort = BestSellerSort.Quantity, TimeZoneInfo? timeZone = null)
    {
        timeZone ??= ShopZone;
        await using var scope = fixture.Services.CreateAsyncScope();
        var bestSellers = await scope.ServiceProvider.GetRequiredService<BestSellerReportQueryHandler>()
            .GetBestSellersAsync(new(first, last, timeZone, sort));
        var revenue = await scope.ServiceProvider.GetRequiredService<RevenueReportQueryHandler>()
            .GetRevenueAsync(new(first, last, timeZone));

        Assert.True(bestSellers.IsSuccess, bestSellers.Error?.Description);
        Assert.True(revenue.IsSuccess, revenue.Error?.Description);
        var report = bestSellers.Value;
        Assert.Equal(revenue.Value.Revenue, report.Items.Sum(row => row.Revenue));
        Assert.Equal(revenue.Value.Revenue, report.Variants.Sum(row => row.Revenue));
        Assert.Equal(report.Items.Sum(row => row.Quantity), report.Variants.Sum(row => row.Quantity));
        Assert.Equal(first, report.FirstDate);
        Assert.Equal(last, report.LastDateInclusive);
        Assert.Equal(timeZone.Id, report.TimeZoneId);
        return report;
    }
}
