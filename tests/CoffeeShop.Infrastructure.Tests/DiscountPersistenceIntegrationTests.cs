namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

public sealed class DiscountPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly string databaseName = $"coffeeshop_discounts_{Guid.NewGuid():N}";
    private readonly EntityId actorId = EntityId.New();
    private ServiceProvider services = null!;
    private string adminConnectionString = null!;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(CatalogPersistenceIntegrationTests.ConnectionVariable);
        if (string.IsNullOrWhiteSpace(configured)) return;
        adminConnectionString = configured;
        await using var connection = new NpgsqlConnection(configured);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = databaseName, Pooling = false };
        services = new ServiceCollection().AddCoffeeShopPostgres(builder.ConnectionString)
            .AddSingleton<IClock, SystemClock>()
            .AddSingleton<ICurrentUser>(new DiscountCurrentUser(actorId))
            .AddScoped<DiscountCommandHandler>()
            .BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        await context.Database.MigrateAsync();
        var now = new SystemClock().UtcNow;
        context.Users.Add(new User(actorId, "discount_actor", "discounts@example.com",
            "not-a-real-password-hash", "Discount Actor", null, now, now));
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (services is not null) await services.DisposeAsync();
        if (adminConnectionString is null) return;
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task RequestsApprovalsAndRejections_PersistReasonActorsAndAmountsAcrossScopes()
    {
        var orderId = await InsertOrderAsync();
        var fixedId = await RequestAsync(orderId, DiscountType.Fixed, 20m);
        var percentageId = await RequestAsync(orderId, DiscountType.Percentage, 50m);
        var rejectedId = await RequestAsync(orderId, DiscountType.Fixed, 5m);

        await using (var scope = services.CreateAsyncScope())
        {
            var order = await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Orders
                .Include(item => item.Discounts).SingleAsync(item => item.Id == orderId);
            Assert.Equal(100m, order.Total.Amount);
            Assert.Equal(Money.Zero, order.DiscountTotal);
            Assert.All(order.Discounts, discount =>
            {
                Assert.Equal(DiscountStatus.Pending, discount.Status);
                Assert.Equal("Service recovery", discount.Reason);
                Assert.Equal(actorId, discount.RequestedBy);
                Assert.Null(discount.ComputedAmount);
            });
        }

        Assert.True((await DecideAsync(fixedId)).IsSuccess);
        Assert.True((await DecideAsync(percentageId)).IsSuccess);
        Assert.True((await DecideAsync(rejectedId, approve: false)).IsSuccess);

        await using var verifyScope = services.CreateAsyncScope();
        var stored = await verifyScope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Orders
            .Include(order => order.Discounts).SingleAsync(order => order.Id == orderId);
        Assert.Equal(40m, stored.Total.Amount);
        Assert.Equal(60m, stored.DiscountTotal.Amount);
        Assert.Equal(40m, stored.Discounts.Single(discount => discount.Id == percentageId).ComputedAmount!.Amount);
        Assert.All(stored.Discounts.Where(discount => discount.Status == DiscountStatus.Approved), discount =>
        {
            Assert.Equal(actorId, discount.ApprovedBy);
            Assert.True(discount.ApprovedAt!.Value >= discount.RequestedAt.Value);
        });
        var rejected = stored.Discounts.Single(discount => discount.Id == rejectedId);
        Assert.Equal(actorId, rejected.RejectedBy);
        Assert.NotNull(rejected.RejectedAt);
        Assert.Null(rejected.ComputedAmount);
    }

    [PostgresFact]
    public async Task Approval_AfterPartialPaymentUpdatesOutstandingBalanceWithoutChangingPayment()
    {
        var orderId = await InsertOrderAsync();
        var discountId = await RequestAsync(orderId, DiscountType.Fixed, 20m);
        await using (var scope = services.CreateAsyncScope())
        {
            var provider = scope.ServiceProvider;
            var handler = new PaymentCommandHandler(provider.GetRequiredService<IPaymentRepository>(),
                provider.GetRequiredService<IUnitOfWork>(), provider.GetRequiredService<ITransactionManager>(),
                provider.GetRequiredService<IClock>(), provider.GetRequiredService<ICurrentUser>());
            var payment = await handler.RecordAsync(new(EntityId.New(), orderId, PaymentMethod.Cash, 80m));
            Assert.True(payment.IsSuccess, payment.Error?.Description);
            Assert.Equal(20m, payment.Value.OutstandingBalance);
        }
        Assert.True((await DecideAsync(discountId)).IsSuccess);

        await using var verifyScope = services.CreateAsyncScope();
        var stored = await verifyScope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Orders
            .Include(order => order.Payments).SingleAsync(order => order.Id == orderId);
        Assert.Equal(80m, stored.Total.Amount);
        Assert.Equal(80m, stored.PaidAmount.Amount);
        Assert.Equal(80m, Assert.Single(stored.Payments).Amount.Amount);
        Assert.True(stored.IsFullyPaid);
        Assert.Equal(Money.Zero, stored.OutstandingBalance);
        var context = verifyScope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var now = new SystemClock().UtcNow;
        stored.TransitionTo(OrderStatus.Confirmed, actorId, now);
        stored.TransitionTo(OrderStatus.Preparing, actorId, now);
        stored.TransitionTo(OrderStatus.Ready, actorId, now);
        stored.TransitionTo(OrderStatus.Completed, actorId, now);
        await context.SaveChangesAsync();
        Assert.Equal(4, await context.OrderStatusHistories.CountAsync(history => history.OrderId == orderId));
    }

    [PostgresFact]
    public async Task ConcurrentApprovals_CannotReduceTotalBelowZero()
    {
        var orderId = await InsertOrderAsync();
        var first = await RequestAsync(orderId, DiscountType.Fixed, 60m);
        var second = await RequestAsync(orderId, DiscountType.Fixed, 60m);

        var results = await Task.WhenAll(DecideAsync(first), DecideAsync(second));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(ErrorType.BusinessRule, Assert.Single(results, result => result.IsFailure).Error!.Type);
        await using var scope = services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Orders
            .Include(order => order.Discounts).SingleAsync(order => order.Id == orderId);
        Assert.Equal(40m, stored.Total.Amount);
        Assert.Equal(60m, stored.DiscountTotal.Amount);
        Assert.Single(stored.Discounts, discount => discount.Status == DiscountStatus.Approved);
        Assert.Single(stored.Discounts, discount => discount.Status == DiscountStatus.Pending);
    }

    [PostgresFact]
    public async Task ConcurrentApprovalRetries_ApplyDiscountOnlyOnce()
    {
        var orderId = await InsertOrderAsync();
        var discountId = await RequestAsync(orderId, DiscountType.Percentage, 50m);
        var results = await Task.WhenAll(DecideAsync(discountId), DecideAsync(discountId));
        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, Assert.Single(results, result => result.IsFailure).Error!.Type);

        await using var scope = services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Orders
            .SingleAsync(order => order.Id == orderId);
        Assert.Equal(50m, stored.Total.Amount);
        Assert.Equal(50m, stored.DiscountTotal.Amount);
    }

    [PostgresFact]
    public async Task FailureAfterSave_RollsBackApprovalAndTotalTogether()
    {
        var orderId = await InsertOrderAsync();
        var discountId = await RequestAsync(orderId, DiscountType.Fixed, 20m);
        await using (var scope = services.CreateAsyncScope())
        {
            var provider = scope.ServiceProvider;
            var context = provider.GetRequiredService<CoffeeShopDbContext>();
            var handler = new DiscountCommandHandler(provider.GetRequiredService<IDiscountRepository>(),
                new SaveThenFail(context), provider.GetRequiredService<ITransactionManager>(),
                provider.GetRequiredService<IClock>(), provider.GetRequiredService<ICurrentUser>());
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ApproveAsync(new(discountId)));
        }

        await using var verifyScope = services.CreateAsyncScope();
        var stored = await verifyScope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Orders
            .Include(order => order.Discounts).SingleAsync(order => order.Id == orderId);
        Assert.Equal(100m, stored.Total.Amount);
        Assert.Equal(Money.Zero, stored.DiscountTotal);
        var discount = Assert.Single(stored.Discounts);
        Assert.Equal(DiscountStatus.Pending, discount.Status);
        Assert.Null(discount.ApprovedBy);
        Assert.Null(discount.ComputedAmount);
    }

    [PostgresTheory]
    [InlineData("percentage", "CK_discounts_percentage")]
    [InlineData("reason", "CK_discounts_reason")]
    [InlineData("approval", "CK_discounts_decision")]
    [InlineData("rejection", "CK_discounts_decision")]
    public async Task DatabaseConstraints_RejectInvalidDiscountWrites(string invalidField, string constraintName)
    {
        var orderId = await InsertOrderAsync();
        var discountId = await RequestAsync(orderId, DiscountType.Percentage, 10m);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            switch (invalidField)
            {
                case "percentage":
                    await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.discounts SET value = 101 WHERE id = {discountId.Value}");
                    break;
                case "reason":
                    await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.discounts SET reason = ' ' WHERE id = {discountId.Value}");
                    break;
                case "approval":
                    await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.discounts SET status = 'approved' WHERE id = {discountId.Value}");
                    break;
                default:
                    await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.discounts SET status = 'rejected' WHERE id = {discountId.Value}");
                    break;
            }
        });
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal(constraintName, exception.ConstraintName);
    }

    [PostgresFact]
    public async Task Migration_PreservesLegacyRequestsWithoutInventingRejectionActors()
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var migrator = context.GetService<IMigrator>();
        var migrations = context.Database.GetMigrations().ToArray();
        var discountMigrationIndex = Array.FindIndex(migrations,
            migration => migration.EndsWith("_AddDiscountApprovalRules", StringComparison.Ordinal));
        Assert.True(discountMigrationIndex > 0);
        var previous = migrations[discountMigrationIndex - 1];
        await migrator.MigrateAsync(previous);
        var orderId = await InsertOrderAsync();
        var legacyId = EntityId.New();
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO sales.discounts (id, order_id, type, value, status, requested_by, requested_at)
            VALUES ({legacyId.Value}, {orderId.Value}, 'fixed', 10, 'rejected', {actorId.Value}, {DateTimeOffset.UtcNow})");

        await migrator.MigrateAsync();
        var legacy = await context.Discounts.AsNoTracking().SingleAsync(discount => discount.Id == legacyId);
        Assert.Equal("Legacy discount request (reason not recorded)", legacy.Reason);
        Assert.Equal(DiscountStatus.Rejected, legacy.Status);
        Assert.Null(legacy.RejectedBy);
        Assert.Null(legacy.RejectedAt);
        Assert.Equal(100m, (await context.Orders.AsNoTracking().SingleAsync(order => order.Id == orderId)).Total.Amount);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        await context.Database.MigrateAsync();
    }

    private async Task<EntityId> InsertOrderAsync()
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var id = EntityId.New();
        var number = $"DISC-{Guid.NewGuid():N}"[..20];
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO sales.orders (id, order_number, channel, status, subtotal, total, created_by, created_at)
            VALUES ({id.Value}, {number}, 'takeaway', 'draft', 100, 100, {actorId.Value}, {DateTimeOffset.UtcNow})");
        return id;
    }

    private async Task<EntityId> RequestAsync(EntityId orderId, string type, decimal value)
    {
        await using var scope = services.CreateAsyncScope();
        var id = EntityId.New();
        var result = await scope.ServiceProvider.GetRequiredService<DiscountCommandHandler>()
            .RequestAsync(new(id, orderId, type, value, "Service recovery"));
        Assert.True(result.IsSuccess, result.Error?.Description);
        return id;
    }

    private async Task<Result<DiscountOperationResult>> DecideAsync(EntityId discountId, bool approve = true)
    {
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DiscountCommandHandler>();
        return approve ? await handler.ApproveAsync(new(discountId)) : await handler.RejectAsync(new(discountId));
    }

    private sealed class SaveThenFail(CoffeeShopDbContext context) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Simulated failure after saving approval.");
        }
    }

    private sealed class DiscountCurrentUser(EntityId id) : ICurrentUser
    {
        public EntityId? Id => id;
        public string? Username => "discount_actor";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => ["Manager"];
        public IReadOnlyCollection<string> Permissions => ["discounts.request", "discounts.approve"];
        public bool HasPermission(string permission) => Permissions.Contains(permission);
        public bool IsInRole(string role) => Roles.Contains(role);
        public EntityId GetRequiredId() => id;
    }
}
