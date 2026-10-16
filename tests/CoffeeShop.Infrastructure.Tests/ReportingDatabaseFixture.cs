namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

[CollectionDefinition(nameof(ReportingDatabaseCollection))]
public sealed class ReportingDatabaseCollection : ICollectionFixture<ReportingDatabaseFixture>
{
}

/// <summary>
/// One read-only dataset shared by revenue and channel reports. October recognizes 130 across three
/// orders and November recognizes 40 across one order; invalid projections contribute to neither.
/// </summary>
public sealed class ReportingDatabaseFixture : IAsyncLifetime
{
    private readonly string databaseName = $"coffeeshop_reporting_{Guid.NewGuid():N}";
    private readonly EntityId actorId = EntityId.New();
    private readonly EntityId tableId = EntityId.New();
    private string adminConnectionString = null!;

    public ServiceProvider Services { get; private set; } = null!;

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
        Services = new ServiceCollection().AddCoffeeShopPostgres(builder.ConnectionString).BuildServiceProvider();
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        await context.Database.MigrateAsync();
        var now = AtUtc(2026, 9, 30, 0);
        context.Users.Add(new User(
            actorId, "revenue_actor", "revenue@example.com", "not-a-real-password-hash",
            "Revenue Actor", null, now, now));
        context.DiningTables.Add(new DiningTable(tableId, "REPORT-1", 2));
        await context.SaveChangesAsync();
        await SeedKnownOrdersAsync(context);
    }

    public async Task DisposeAsync()
    {
        if (Services is not null)
        {
            await Services.DisposeAsync();
        }

        if (adminConnectionString is not null)
        {
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task SeedKnownOrdersAsync(CoffeeShopDbContext context)
    {
        var split = await InsertOrderAsync(
            context, "REV-SPLIT", "completed", 100m, 120m, AtUtc(2026, 10, 1, 2), channel: "dine_in");
        await InsertPaymentAsync(context, split, PaymentMethod.Cash, 80m, PaymentStatus.Succeeded, AtUtc(2026, 9, 30, 16, 59, 59));
        await InsertPaymentAsync(context, split, PaymentMethod.Card, 40m, PaymentStatus.Succeeded, AtUtc(2026, 9, 30, 17));

        var discounted = await InsertOrderAsync(
            context, "REV-DISCOUNT", "completed", 80m, 100m, AtUtc(2026, 10, 15, 4),
            subtotal: 100m, discountTotal: 20m);
        await InsertPaymentAsync(context, discounted, PaymentMethod.Card, 50m, PaymentStatus.Succeeded, AtUtc(2026, 10, 15, 2));
        await InsertPaymentAsync(context, discounted, PaymentMethod.EWallet, 50m, PaymentStatus.Succeeded, AtUtc(2026, 10, 15, 3));

        var monthEnd = await InsertOrderAsync(
            context, "REV-MONTH-END", "completed", 30m, 30m, AtUtc(2026, 10, 31, 17), channel: "delivery");
        await InsertPaymentAsync(context, monthEnd, PaymentMethod.EWallet, 30m, PaymentStatus.Succeeded, AtUtc(2026, 10, 31, 16, 59, 59));

        var nextMonth = await InsertOrderAsync(context, "REV-NEXT-MONTH", "completed", 40m, 40m, AtUtc(2026, 10, 31, 18));
        await InsertPaymentAsync(context, nextMonth, PaymentMethod.Cash, 40m, PaymentStatus.Succeeded, AtUtc(2026, 10, 31, 17));

        var cancelled = await InsertOrderAsync(
            context, "REV-CANCELLED", "cancelled", 60m, 60m,
            completedAt: null, cancelledAt: AtUtc(2026, 10, 20, 3), channel: "delivery");
        await InsertPaymentAsync(context, cancelled, PaymentMethod.Cash, 60m, PaymentStatus.Succeeded, AtUtc(2026, 10, 20, 2));

        var restored = await InsertOrderAsync(
            context, "REV-RESTORED", "completed", 70m, 70m, AtUtc(2026, 10, 21, 3), channel: "dine_in");
        await InsertPaymentAsync(context, restored, PaymentMethod.Cash, 70m, PaymentStatus.Succeeded, AtUtc(2026, 10, 21, 2));
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO inventory.order_inventory_restorations (order_id, restored_at, restored_by)
            VALUES ({restored.Value}, {AtUtc(2026, 10, 21, 4).Value}, {actorId.Value})");

        var underpaid = await InsertOrderAsync(context, "REV-UNDERPAID", "completed", 100m, 90m, AtUtc(2026, 10, 22, 3));
        await InsertPaymentAsync(context, underpaid, PaymentMethod.Cash, 90m, PaymentStatus.Succeeded, AtUtc(2026, 10, 22, 2));

        var ready = await InsertOrderAsync(context, "REV-READY", "ready", 50m, 50m, completedAt: null, channel: "delivery");
        await InsertPaymentAsync(context, ready, PaymentMethod.Cash, 50m, PaymentStatus.Succeeded, AtUtc(2026, 10, 23, 2));

        var missingCompletion = await InsertOrderAsync(
            context, "REV-NO-COMPLETION", "completed", 30m, 30m, completedAt: null, channel: "dine_in");
        await InsertPaymentAsync(context, missingCompletion, PaymentMethod.Cash, 30m, PaymentStatus.Succeeded, AtUtc(2026, 10, 23, 2));

        var cancelledProjection = await InsertOrderAsync(
            context, "REV-CANCELLED-PROJECTION", "completed", 60m, 60m,
            AtUtc(2026, 10, 24, 3), cancelledAt: AtUtc(2026, 10, 24, 4), channel: "delivery");
        await InsertPaymentAsync(context, cancelledProjection, PaymentMethod.Cash, 60m, PaymentStatus.Succeeded, AtUtc(2026, 10, 24, 2));

        var latePayment = await InsertOrderAsync(
            context, "REV-LATE-PAYMENT", "completed", 50m, 50m, AtUtc(2026, 10, 25, 3), channel: "dine_in");
        await InsertPaymentAsync(context, latePayment, PaymentMethod.Cash, 50m, PaymentStatus.Succeeded, AtUtc(2026, 10, 25, 4));

        var unsuccessful = await InsertOrderAsync(context, "REV-UNSUCCESSFUL", "completed", 100m, 100m, AtUtc(2026, 10, 25, 3));
        await InsertPaymentAsync(context, unsuccessful, PaymentMethod.Cash, 100m, PaymentStatus.Pending, AtUtc(2026, 10, 25, 1));
        await InsertPaymentAsync(context, unsuccessful, PaymentMethod.Cash, 100m, PaymentStatus.Failed, AtUtc(2026, 10, 25, 2));

        await InsertOrderAsync(
            context, "REV-COMPLIMENTARY", "completed", 0m, 0m, AtUtc(2026, 10, 25, 3),
            subtotal: 100m, discountTotal: 100m, channel: "delivery");
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
        decimal discountTotal = 0m,
        string channel = "takeaway")
    {
        var id = EntityId.New();
        Guid? selectedTableId = channel == "dine_in" ? tableId.Value : null;
        DateTimeOffset? completedAtValue = completedAt?.Value;
        DateTimeOffset? cancelledAtValue = cancelledAt?.Value;
        var cancelReason = cancelledAt is null ? null : "Cancelled";
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO sales.orders
                (id, order_number, channel, status, table_id, subtotal, discount_total, total, paid_amount,
                 loyalty_points_redeemed, loyalty_discount_amount, created_by, created_at,
                 confirmed_at, completed_at, cancelled_at, cancel_reason)
            VALUES
                 ({id.Value}, {orderNumber}, {channel}, {status}, {selectedTableId}, {subtotal ?? total}, {discountTotal}, {total}, {paidAmount},
                 0, 0, {actorId.Value}, {AtUtc(2026, 9, 30, 0).Value},
                 {AtUtc(2026, 9, 30, 1).Value}, {completedAtValue},
                 {cancelledAtValue}, {cancelReason})");
        if (channel == "delivery")
        {
            context.Deliveries.Add(new Delivery(EntityId.New(), id, "Report Recipient", "0901234567", "123 Coffee Street"));
            await context.SaveChangesAsync();
        }

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

    private static UtcTimestamp AtUtc(int year, int month, int day, int hour, int minute = 0, int second = 0) =>
        new(new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero));
}
