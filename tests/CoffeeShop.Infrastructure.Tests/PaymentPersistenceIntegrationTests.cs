namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

public sealed class PaymentPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly string databaseName = $"coffeeshop_payments_{Guid.NewGuid():N}";
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
        services = new ServiceCollection()
            .AddCoffeeShopPostgres(builder.ConnectionString)
            .AddSingleton<IClock, SystemClock>()
            .AddSingleton<ICurrentUser>(new FixedCurrentUser(actorId))
            .AddScoped<PaymentCommandHandler>()
            .BuildServiceProvider();

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        await context.Database.MigrateAsync();
        var now = new SystemClock().UtcNow;
        context.Users.Add(new User(
            actorId,
            "payment_actor",
            "payments@example.com",
            "not-a-real-password-hash",
            "Payment Actor",
            null,
            now,
            now));
        await context.SaveChangesAsync();
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
    public async Task FullPartialAndStatusPayments_PersistAndControlCompletion()
    {
        var orderId = await InsertReadyOrderAsync(100m);
        var cashId = EntityId.New();

        var partial = await RecordAsync(new(cashId, orderId, PaymentMethod.Cash, 40m));
        var pending = await RecordAsync(new(
            EntityId.New(), orderId, PaymentMethod.Card, 60m, PaymentStatus.Pending, "CARD-PENDING"));
        var failed = await RecordAsync(new(
            EntityId.New(), orderId, PaymentMethod.EWallet, 60m, PaymentStatus.Failed, "WALLET-FAILED"));
        var retry = await RecordAsync(new(cashId, orderId, PaymentMethod.Cash, 40m));
        var full = await RecordAsync(new(
            EntityId.New(), orderId, PaymentMethod.Card, 60m, PaymentStatus.Succeeded, "CARD-SUCCEEDED"));

        Assert.Equal(60m, partial.OutstandingBalance);
        Assert.False(partial.IsFullyPaid);
        Assert.Equal(40m, pending.PaidAmount);
        Assert.Equal(40m, failed.PaidAmount);
        Assert.True(retry.AlreadyRecorded);
        Assert.True(full.IsFullyPaid);
        Assert.Equal(0m, full.OutstandingBalance);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var order = await context.Orders.Include(item => item.Payments).SingleAsync(item => item.Id == orderId);
        Assert.Equal(4, order.Payments.Count);
        Assert.Equal(100m, order.PaidAmount.Amount);
        order.TransitionTo(OrderStatus.Completed, actorId, new SystemClock().UtcNow);
        await context.SaveChangesAsync();
        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [PostgresFact]
    public async Task PartialPayment_RemainsUnpaidAfterReloadAndCannotComplete()
    {
        var orderId = await InsertReadyOrderAsync(100m);
        await RecordAsync(new(EntityId.New(), orderId, PaymentMethod.Cash, 99m));

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var order = await context.Orders.Include(item => item.Payments).SingleAsync(item => item.Id == orderId);

        Assert.Equal(1m, order.OutstandingBalance.Amount);
        Assert.Throws<InvalidOperationException>(() =>
            order.TransitionTo(OrderStatus.Completed, actorId, new SystemClock().UtcNow));
        Assert.Equal(OrderStatus.Ready, order.Status);
    }

    [PostgresFact]
    public async Task ConcurrentDuplicateRetry_RecordsBalanceOnce()
    {
        var orderId = await InsertReadyOrderAsync(100m);
        var paymentId = EntityId.New();
        var command = new RecordPaymentCommand(
            paymentId, orderId, PaymentMethod.EWallet, 100m, PaymentStatus.Succeeded, "WALLET-RETRY");

        var results = await Task.WhenAll(RecordAsync(command), RecordAsync(command));

        Assert.Single(results, result => !result.AlreadyRecorded);
        Assert.Single(results, result => result.AlreadyRecorded);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        Assert.Single(await context.Payments.AsNoTracking().Where(payment => payment.OrderId == orderId).ToListAsync());
        Assert.Equal(100m, (await context.Orders.AsNoTracking().SingleAsync(order => order.Id == orderId)).PaidAmount.Amount);
    }

    [PostgresFact]
    public async Task DatabaseConstraint_RejectsCardWithoutReference()
    {
        var orderId = await InsertReadyOrderAsync(100m);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var paymentId = EntityId.New();

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO sales.payments
                    (id, order_id, method, amount, status, reference, paid_at, recorded_by)
                VALUES
                    ({paymentId.Value}, {orderId.Value}, 'card', 10, 'succeeded', NULL,
                     {DateTimeOffset.UtcNow}, {actorId.Value})"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("CK_payments_required_reference", exception.ConstraintName);
    }

    private async Task<EntityId> InsertReadyOrderAsync(decimal total)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var id = EntityId.New();
        var number = $"PAY-{Guid.NewGuid():N}"[..20];
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO sales.orders
                (id, order_number, channel, status, subtotal, discount_total, total, paid_amount,
                 loyalty_points_redeemed, loyalty_discount_amount, created_by, created_at, confirmed_at)
            VALUES
                ({id.Value}, {number}, 'takeaway', 'ready', {total}, 0, {total}, 0,
                 0, 0, {actorId.Value}, {DateTimeOffset.UtcNow}, {DateTimeOffset.UtcNow})");
        return id;
    }

    private async Task<PaymentOperationResult> RecordAsync(RecordPaymentCommand command)
    {
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<PaymentCommandHandler>().RecordAsync(command);
        Assert.True(result.IsSuccess, result.Error?.Description);
        return result.Value;
    }

    private sealed class FixedCurrentUser(EntityId id) : ICurrentUser
    {
        public EntityId? Id { get; } = id;
        public string? Username => "payment_actor";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> Permissions => [];
        public bool HasPermission(string permission) => false;
        public bool IsInRole(string role) => false;
        public EntityId GetRequiredId() => Id!;
    }
}
