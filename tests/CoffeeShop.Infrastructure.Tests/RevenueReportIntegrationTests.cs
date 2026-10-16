namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Reporting;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Seeded PostgreSQL verification for known revenue totals and local calendar-month boundaries.
/// Requires PostgreSQL reachable at COFFEESHOP_TEST_POSTGRES; tests skip otherwise.
/// </summary>
[Collection(nameof(ReportingDatabaseCollection))]
public sealed class RevenueReportIntegrationTests(ReportingDatabaseFixture fixture)
{
    private const string TimeZoneId = "Asia/Ho_Chi_Minh";

    [PostgresFact]
    public async Task OctoberReport_ReturnsKnownTotalsAndBothBreakdownsAcrossUtcBoundaries()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<RevenueReportQueryHandler>();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

        var result = await handler.GetRevenueAsync(new(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), timeZone));

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(130m, result.Value.Revenue);
        Assert.Equal(3, result.Value.OrderCount);
        Assert.Equal(AtUtc(2026, 9, 30, 17).Value, result.Value.StartUtc);
        Assert.Equal(AtUtc(2026, 10, 31, 17).Value, result.Value.EndUtcExclusive);
        Assert.Equal(
            [new DailyRevenueBreakdown(new DateOnly(2026, 10, 1), 20m, 1),
             new DailyRevenueBreakdown(new DateOnly(2026, 10, 15), 80m, 1),
             new DailyRevenueBreakdown(new DateOnly(2026, 10, 31), 30m, 1)],
            result.Value.Daily);
        Assert.Equal(
            [new PaymentMethodRevenueBreakdown(PaymentMethod.Card, 70m, 2),
             new PaymentMethodRevenueBreakdown(PaymentMethod.EWallet, 60m, 2)],
            result.Value.ByPaymentMethod);
    }

    [PostgresFact]
    public async Task AdjacentNovemberReport_DoesNotDoubleCountTheOctoberBoundary()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<RevenueReportQueryHandler>();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

        var result = await handler.GetRevenueAsync(new(
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), timeZone));

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(40m, result.Value.Revenue);
        Assert.Equal(1, result.Value.OrderCount);
        Assert.Equal(
            [new DailyRevenueBreakdown(new DateOnly(2026, 11, 1), 40m, 1)],
            result.Value.Daily);
        Assert.Equal(
            [new PaymentMethodRevenueBreakdown(PaymentMethod.Cash, 40m, 1)],
            result.Value.ByPaymentMethod);
    }

    private static UtcTimestamp AtUtc(
        int year,
        int month,
        int day,
        int hour,
        int minute = 0,
        int second = 0) =>
        new(new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero));
}
