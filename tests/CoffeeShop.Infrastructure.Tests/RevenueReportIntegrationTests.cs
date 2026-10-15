namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Reporting;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

/// <summary>
/// Seeded PostgreSQL verification for known revenue totals and local calendar-month boundaries.
/// Requires PostgreSQL reachable at COFFEESHOP_TEST_POSTGRES; tests skip otherwise.
/// </summary>
public sealed class RevenueReportIntegrationTests : IAsyncLifetime
{
    private const string TimeZoneId = "Asia/Ho_Chi_Minh";
    private readonly string databaseName = $"coffeeshop_revenue_{Guid.NewGuid():N}";
    private readonly EntityId actorId = EntityId.New();
    private ServiceProvider services = null!;
    private string adminConnectionString = null!;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(CatalogPersistenceIntegrationTests.ConnectionVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        adminConnectionString = configured;
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = databaseName, Pooling = false };
        services = new ServiceCollection().AddCoffeeShopPostgres(builder.ConnectionString).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        await context.Database.MigrateAsync();
        var now = AtUtc(2026, 9, 30, 0);
        context.Users.Add(new User(
            actorId,
            "revenue_actor",
            "revenue@example.com",
            "not-a-real-password-hash",
            "Revenue Actor",
            null,
            now,
            now));
        await context.SaveChangesAsync();
        await SeedKnownOrdersAsync(context);
    }

    public async Task DisposeAsync()
    {
        if (services is not null)
        {
            await services.DisposeAsync();
        }

        if (adminConnectionString is not null)
        {
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    [PostgresFact]
    public async Task OctoberReport_ReturnsKnownTotalsAndBothBreakdownsAcrossUtcBoundaries()
    {
        await using var scope = services.CreateAsyncScope();
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
        await using var scope = services.CreateAsyncScope();
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

    private async Task SeedKnownOrdersAsync(CoffeeShopDbContext context)
    {
        var split = await InsertOrderAsync(context, "REV-SPLIT", "completed", 100m, 120m, AtUtc(2026, 10, 1, 2));
        await InsertPaymentAsync(context, split, PaymentMethod.Cash, 80m, PaymentStatus.Succeeded, AtUtc(2026, 9, 30, 16, 59, 59));
        await InsertPaymentAsync(context, split, PaymentMethod.Card, 40m, PaymentStatus.Succeeded, AtUtc(2026, 9, 30, 17));

        var discounted = await InsertOrderAsync(
            context, "REV-DISCOUNT", "completed", 80m, 100m, AtUtc(2026, 10, 15, 4),
            subtotal: 100m, discountTotal: 20m);
        await InsertPaymentAsync(context, discounted, PaymentMethod.Card, 50m, PaymentStatus.Succeeded, AtUtc(2026, 10, 15, 2));
        await InsertPaymentAsync(context, discounted, PaymentMethod.EWallet, 50m, PaymentStatus.Succeeded, AtUtc(2026, 10, 15, 3));

        var monthEnd = await InsertOrderAsync(context, "REV-MONTH-END", "completed", 30m, 30m, AtUtc(2026, 10, 31, 17));
        await InsertPaymentAsync(context, monthEnd, PaymentMethod.EWallet, 30m, PaymentStatus.Succeeded, AtUtc(2026, 10, 31, 16, 59, 59));

        var nextMonth = await InsertOrderAsync(context, "REV-NEXT-MONTH", "completed", 40m, 40m, AtUtc(2026, 10, 31, 18));
        await InsertPaymentAsync(context, nextMonth, PaymentMethod.Cash, 40m, PaymentStatus.Succeeded, AtUtc(2026, 10, 31, 17));

        var cancelled = await InsertOrderAsync(
            context, "REV-CANCELLED", "cancelled", 60m, 60m,
            completedAt: null, cancelledAt: AtUtc(2026, 10, 20, 3));
        await InsertPaymentAsync(context, cancelled, PaymentMethod.Cash, 60m, PaymentStatus.Succeeded, AtUtc(2026, 10, 20, 2));

        var restored = await InsertOrderAsync(context, "REV-RESTORED", "completed", 70m, 70m, AtUtc(2026, 10, 21, 3));
        await InsertPaymentAsync(context, restored, PaymentMethod.Cash, 70m, PaymentStatus.Succeeded, AtUtc(2026, 10, 21, 2));
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO inventory.order_inventory_restorations (order_id, restored_at, restored_by)
            VALUES ({restored.Value}, {AtUtc(2026, 10, 21, 4).Value}, {actorId.Value})");

        var underpaid = await InsertOrderAsync(context, "REV-UNDERPAID", "completed", 100m, 90m, AtUtc(2026, 10, 22, 3));
        await InsertPaymentAsync(context, underpaid, PaymentMethod.Cash, 90m, PaymentStatus.Succeeded, AtUtc(2026, 10, 22, 2));
    }

    private async Task<EntityId> InsertOrderAsync(
        CoffeeShopDbContext context,
        string orderNumber,
        string status,
        decimal total,
        decimal paidAmount,
        UtcTimestamp? completedAt,
        UtcTimestamp? cancelledAt = null,
        decimal? subtotal = null,
        decimal discountTotal = 0m)
    {
        var id = EntityId.New();
        DateTimeOffset? completedAtValue = completedAt?.Value;
        DateTimeOffset? cancelledAtValue = cancelledAt?.Value;
        var cancelReason = cancelledAt is null ? null : "Cancelled";
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO sales.orders
                (id, order_number, channel, status, subtotal, discount_total, total, paid_amount,
                 loyalty_points_redeemed, loyalty_discount_amount, created_by, created_at,
                 confirmed_at, completed_at, cancelled_at, cancel_reason)
            VALUES
                 ({id.Value}, {orderNumber}, 'takeaway', {status}, {subtotal ?? total}, {discountTotal}, {total}, {paidAmount},
                 0, 0, {actorId.Value}, {AtUtc(2026, 9, 30, 0).Value},
                 {AtUtc(2026, 9, 30, 1).Value}, {completedAtValue},
                 {cancelledAtValue}, {cancelReason})");
        return id;
    }

    private async Task InsertPaymentAsync(
        CoffeeShopDbContext context,
        EntityId orderId,
        string method,
        decimal amount,
        string status,
        UtcTimestamp paidAt)
    {
        var reference = method == PaymentMethod.Cash ? null : $"REF-{Guid.NewGuid():N}";
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO sales.payments
                (id, order_id, method, amount, status, reference, paid_at, recorded_by)
            VALUES
                ({Guid.NewGuid()}, {orderId.Value}, {method}, {amount}, {status}, {reference},
                 {paidAt.Value}, {actorId.Value})");
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
