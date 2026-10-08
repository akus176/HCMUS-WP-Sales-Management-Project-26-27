namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Customers;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

public sealed class CustomerLoyaltyPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly string databaseName = $"coffeeshop_loyalty_{Guid.NewGuid():N}";
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
            .AddScoped<CustomerCommandHandler>()
            .AddScoped<CustomerQueryHandler>()
            .AddScoped<LoyaltyCommandHandler>()
            .BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        await context.Database.MigrateAsync();
        var now = new SystemClock().UtcNow;
        context.Users.Add(new User(
            actorId,
            "loyalty_actor",
            "loyalty@example.com",
            "not-a-real-password-hash",
            "Loyalty Actor",
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
    public async Task CustomerProfileAndAddresses_PersistAcrossScopesAndRemainSearchableAfterDeactivation()
    {
        EntityId customerId;
        EntityId addressId;
        await using (var scope = services.CreateAsyncScope())
        {
            var commands = scope.ServiceProvider.GetRequiredService<CustomerCommandHandler>();
            customerId = (await commands.CreateCustomerAsync(new(
                " Alice Nguyen ", "0901234567", "ALICE@EXAMPLE.COM"))).Value;
            addressId = (await commands.AddAddressAsync(new(
                customerId, "1 Main Street", "Home", IsDefault: true))).Value;
            Assert.True((await commands.UpdateAddressAsync(new(
                customerId, addressId, "11 Main Street", "Primary", IsDefault: true))).IsSuccess);
            Assert.True((await commands.DeactivateCustomerAsync(new(customerId))).IsSuccess);
        }

        await using var readScope = services.CreateAsyncScope();
        var queries = readScope.ServiceProvider.GetRequiredService<CustomerQueryHandler>();
        Assert.Empty((await queries.SearchCustomersAsync(new("Alice"))).Value);
        var found = Assert.Single((await queries.SearchCustomersAsync(new("alice", IncludeInactive: true))).Value);
        Assert.Equal(customerId, found.Id);
        Assert.False(found.IsActive);
        var detail = (await queries.GetCustomerDetailAsync(customerId)).Value;
        Assert.Equal("alice@example.com", detail.Email);
        var address = Assert.Single(detail.Addresses);
        Assert.Equal(addressId, address.Id);
        Assert.Equal("11 Main Street", address.AddressLine);
        Assert.True(address.IsDefault);
        Assert.Empty(readScope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().ChangeTracker.Entries());
    }

    [PostgresFact]
    public async Task EarningAndRedemption_PersistOnceAndHistoryReturnsBothEntries()
    {
        await ConfigureSettingsAsync();
        var customerId = await CreateCustomerAsync("0901111111");
        var earnOrder = await InsertOrderAsync(customerId, "completed", total: 100m, paid: 100m);
        var redeemOrder = await InsertOrderAsync(customerId, "draft", total: 100m, paid: 0m);

        var firstEarn = await EarnAsync(earnOrder);
        var retryEarn = await EarnAsync(earnOrder);
        var firstRedeem = await RedeemAsync(redeemOrder, 5);
        var retryRedeem = await RedeemAsync(redeemOrder, 5);

        Assert.False(firstEarn.AlreadyApplied);
        Assert.True(retryEarn.AlreadyApplied);
        Assert.False(firstRedeem.AlreadyApplied);
        Assert.True(retryRedeem.AlreadyApplied);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var customer = await context.Customers.AsNoTracking().SingleAsync(item => item.Id == customerId);
        var order = await context.Orders.AsNoTracking().SingleAsync(item => item.Id == redeemOrder);
        Assert.Equal(5, customer.LoyaltyPointsBalance);
        Assert.Equal(5, order.LoyaltyPointsRedeemed);
        Assert.Equal(5m, order.LoyaltyDiscountAmount.Amount);
        Assert.Equal(95m, order.Total.Amount);
        var history = (await scope.ServiceProvider.GetRequiredService<CustomerQueryHandler>()
            .GetLoyaltyHistoryAsync(customerId)).Value;
        Assert.Equal(2, history.TotalCount);
        Assert.Contains(history.Items, item => item.Type == "earn" && item.OrderId == earnOrder && item.Points == 10);
        Assert.Contains(history.Items, item => item.Type == "redeem" && item.OrderId == redeemOrder && item.Points == -5);
    }

    [PostgresFact]
    public async Task CancellationReversal_RestoresBalancesOnceAndPreservesAppendOnlyHistory()
    {
        await ConfigureSettingsAsync();
        var customerId = await CreateCustomerAsync("0902222222");
        var earnOrder = await InsertOrderAsync(customerId, "completed", total: 200m, paid: 200m);
        var redeemOrder = await InsertOrderAsync(customerId, "draft", total: 100m, paid: 0m);
        await EarnAsync(earnOrder);
        await RedeemAsync(redeemOrder, 10);
        await MarkCancelledAsync(earnOrder, redeemOrder);

        var firstRedeemReversal = await ReverseAsync(redeemOrder);
        var retryRedeemReversal = await ReverseAsync(redeemOrder);
        var firstEarnReversal = await ReverseAsync(earnOrder);
        var retryEarnReversal = await ReverseAsync(earnOrder);

        Assert.False(firstRedeemReversal.AlreadyApplied);
        Assert.True(retryRedeemReversal.AlreadyApplied);
        Assert.False(firstEarnReversal.AlreadyApplied);
        Assert.True(retryEarnReversal.AlreadyApplied);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        Assert.Equal(0, (await context.Customers.AsNoTracking().SingleAsync(item => item.Id == customerId)).LoyaltyPointsBalance);
        var history = (await scope.ServiceProvider.GetRequiredService<CustomerQueryHandler>()
            .GetLoyaltyHistoryAsync(customerId)).Value;
        Assert.Equal(4, history.TotalCount);
        Assert.Single(history.Items, item => item.Type == "reverse_earn");
        Assert.Single(history.Items, item => item.Type == "reverse_redeem");
        Assert.All(history.Items.Where(item => item.Type.StartsWith("reverse_", StringComparison.Ordinal)),
            item => Assert.NotNull(item.ReversesTransactionId));
    }

    [PostgresFact]
    public async Task ConcurrentRedemptionRetries_SerializeAndApplyBalanceOnce()
    {
        await ConfigureSettingsAsync();
        var customerId = await CreateCustomerAsync("0903333333");
        var earnOrder = await InsertOrderAsync(customerId, "completed", total: 1000m, paid: 1000m);
        await EarnAsync(earnOrder);
        var redeemOrder = await InsertOrderAsync(customerId, "draft", total: 200m, paid: 0m);

        async Task<LoyaltyOperationResult> RedeemInScopeAsync()
        {
            await using var scope = services.CreateAsyncScope();
            return (await scope.ServiceProvider.GetRequiredService<LoyaltyCommandHandler>()
                .RedeemForOrderAsync(new(redeemOrder, 20))).Value;
        }

        var results = await Task.WhenAll(RedeemInScopeAsync(), RedeemInScopeAsync());

        Assert.Single(results, result => !result.AlreadyApplied);
        Assert.Single(results, result => result.AlreadyApplied);
        await using var verifyScope = services.CreateAsyncScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        Assert.Equal(80, (await context.Customers.AsNoTracking().SingleAsync(item => item.Id == customerId)).LoyaltyPointsBalance);
        Assert.Single(await context.LoyaltyTransactions.AsNoTracking()
            .Where(item => item.OrderId == redeemOrder && item.Type == "redeem").ToListAsync());
    }

    private async Task ConfigureSettingsAsync()
    {
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<LoyaltyCommandHandler>()
            .UpdateSettingsAsync(new(0.1m, 1m, 50m));
        Assert.True(result.IsSuccess);
    }

    private async Task<EntityId> CreateCustomerAsync(string phone)
    {
        await using var scope = services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<CustomerCommandHandler>()
            .CreateCustomerAsync(new("Integration Customer", phone))).Value;
    }

    private async Task<EntityId> InsertOrderAsync(
        EntityId customerId,
        string status,
        decimal total,
        decimal paid)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var id = EntityId.New();
        var number = $"ORD-{Guid.NewGuid():N}"[..20];
        var now = DateTimeOffset.UtcNow;
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO sales.orders
                (id, order_number, channel, status, customer_id, subtotal, discount_total, total,
                 paid_amount, loyalty_points_redeemed, loyalty_discount_amount, created_by, created_at)
            VALUES
                ({id.Value}, {number}, 'takeaway', {status}, {customerId.Value}, {total}, 0, {total},
                 {paid}, 0, 0, {actorId.Value}, {now})");
        return id;
    }

    private async Task MarkCancelledAsync(params EntityId[] orderIds)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        foreach (var orderId in orderIds)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE sales.orders SET status = 'cancelled', cancelled_at = {DateTimeOffset.UtcNow}, cancel_reason = 'Integration cancellation' WHERE id = {orderId.Value}");
        }
    }

    private async Task<LoyaltyOperationResult> EarnAsync(EntityId orderId)
    {
        await using var scope = services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<LoyaltyCommandHandler>()
            .EarnForCompletedOrderAsync(new(orderId))).Value;
    }

    private async Task<LoyaltyOperationResult> RedeemAsync(EntityId orderId, int points)
    {
        await using var scope = services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<LoyaltyCommandHandler>()
            .RedeemForOrderAsync(new(orderId, points))).Value;
    }

    private async Task<LoyaltyReversalResult> ReverseAsync(EntityId orderId)
    {
        await using var scope = services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<LoyaltyCommandHandler>()
            .ReverseForCancelledOrderAsync(new(orderId))).Value;
    }

    private sealed class FixedCurrentUser(EntityId id) : ICurrentUser
    {
        public EntityId? Id { get; } = id;

        public string? Username => "loyalty_actor";

        public bool IsAuthenticated => true;

        public IReadOnlyCollection<string> Roles => [];

        public IReadOnlyCollection<string> Permissions => [];

        public bool HasPermission(string permission) => false;

        public bool IsInRole(string role) => false;

        public EntityId GetRequiredId() => Id!;
    }
}
