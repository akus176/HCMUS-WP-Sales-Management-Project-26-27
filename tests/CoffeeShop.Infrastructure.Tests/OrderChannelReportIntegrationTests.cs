namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Reporting;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Channel and revenue queries run against the same migrated PostgreSQL database and seeded orders.
/// Requires COFFEESHOP_TEST_POSTGRES, matching the existing revenue integration tests.
/// </summary>
[Collection(nameof(ReportingDatabaseCollection))]
public sealed class OrderChannelReportIntegrationTests(ReportingDatabaseFixture fixture)
{
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    [PostgresFact]
    public async Task OctoberReport_ReconcilesAllThreeChannelsWithTheKnownRevenueDataset()
    {
        var report = await AssertReconcilesAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31),
            [new("dine_in", 20m, 1), new("takeaway", 80m, 1), new("delivery", 30m, 1)]);

        Assert.Equal(130m, report.Revenue);
        Assert.Equal(3, report.OrderCount);
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 17, 0, 0, TimeSpan.Zero), report.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 17, 0, 0, TimeSpan.Zero), report.EndUtcExclusive);
    }

    [PostgresFact]
    public async Task NovemberReport_RecognizesTheBoundaryPaymentOnceAndIncludesZeroChannels()
    {
        var report = await AssertReconcilesAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30),
            [new("dine_in", 0m, 0), new("takeaway", 40m, 1), new("delivery", 0m, 0)]);

        Assert.Equal(40m, report.Revenue);
        Assert.Equal(1, report.OrderCount);
    }

    [PostgresTheory]
    [InlineData(9, 30, 80, 0, 0)]
    [InlineData(10, 1, 20, 0, 0)]
    [InlineData(10, 15, 0, 80, 0)]
    [InlineData(10, 31, 0, 0, 30)]
    [InlineData(8, 1, 0, 0, 0)]
    public async Task DailyReport_UsesLocalPaymentDatesAndReturnsKnownValuesOrZeroes(
        int month, int day, int dineIn, int takeaway, int delivery)
    {
        var date = new DateOnly(2026, month, day);
        await AssertReconcilesAsync(date, date,
            [new("dine_in", dineIn, dineIn > 0 ? 1 : 0),
             new("takeaway", takeaway, takeaway > 0 ? 1 : 0),
             new("delivery", delivery, delivery > 0 ? 1 : 0)]);
    }

    [PostgresFact]
    public async Task WiderRange_CountsAnOrderWithPaymentsAcrossMonthsOnceInItsChannel()
    {
        var report = await AssertReconcilesAsync(new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 31),
            [new("dine_in", 100m, 1), new("takeaway", 80m, 1), new("delivery", 30m, 1)]);

        Assert.Equal(210m, report.Revenue);
        Assert.Equal(3, report.OrderCount);
    }

    [PostgresFact]
    public async Task InvalidOnlyRange_ExcludesCancelledRestoredUnderpaidAndInvalidCompletedOrders()
    {
        var report = await AssertReconcilesAsync(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 25),
            [new("dine_in", 0m, 0), new("takeaway", 0m, 0), new("delivery", 0m, 0)]);

        Assert.Equal(0m, report.Revenue);
        Assert.Equal(0, report.OrderCount);
    }

    [PostgresFact]
    public async Task UtcReport_UsesTheRequestedTimeZoneInsteadOfAssumingTheShopOffset()
    {
        await AssertReconcilesAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1),
            [new("dine_in", 0m, 0), new("takeaway", 0m, 0), new("delivery", 0m, 0)], TimeZoneInfo.Utc);
    }

    private async Task<OrderChannelReport> AssertReconcilesAsync(
        DateOnly firstDate,
        DateOnly lastDateInclusive,
        IReadOnlyList<OrderChannelRevenueBreakdown> expectedChannels,
        TimeZoneInfo? timeZone = null)
    {
        timeZone ??= BusinessTimeZone;
        await using var scope = fixture.Services.CreateAsyncScope();
        var channels = await scope.ServiceProvider.GetRequiredService<OrderChannelReportQueryHandler>()
            .GetChannelsAsync(new(firstDate, lastDateInclusive, timeZone));
        var revenue = await scope.ServiceProvider.GetRequiredService<RevenueReportQueryHandler>()
            .GetRevenueAsync(new(firstDate, lastDateInclusive, timeZone));

        Assert.True(channels.IsSuccess, channels.Error?.Description);
        Assert.True(revenue.IsSuccess, revenue.Error?.Description);
        var report = channels.Value;
        Assert.Equal(expectedChannels, report.ByChannel);
        Assert.Equal(revenue.Value.Revenue, report.Revenue);
        Assert.Equal(revenue.Value.OrderCount, report.OrderCount);
        Assert.Equal(report.Revenue, report.ByChannel.Sum(channel => channel.Revenue));
        Assert.Equal(report.OrderCount, report.ByChannel.Sum(channel => channel.OrderCount));
        Assert.Equal(revenue.Value.StartUtc, report.StartUtc);
        Assert.Equal(revenue.Value.EndUtcExclusive, report.EndUtcExclusive);
        Assert.Equal(firstDate, report.FirstDate);
        Assert.Equal(lastDateInclusive, report.LastDateInclusive);
        Assert.Equal(timeZone.Id, report.TimeZoneId);
        return report;
    }
}
