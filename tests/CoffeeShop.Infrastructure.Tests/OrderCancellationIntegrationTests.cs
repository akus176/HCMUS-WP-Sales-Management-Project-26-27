namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

public sealed class OrderCancellationIntegrationTests : IAsyncLifetime
{
    private readonly string databaseName = $"coffeeshop_cancel_{Guid.NewGuid():N}";
    private readonly EntityId actorId = EntityId.New();
    private static readonly UtcTimestamp Now = new(DateTimeOffset.UtcNow);
    private ServiceProvider services = null!;
    private string? adminConnectionString;
    private string? testConnectionString;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(CatalogPersistenceIntegrationTests.ConnectionVariable);
        if (string.IsNullOrWhiteSpace(configured)) return;
        adminConnectionString = configured;
        await using var connection = new NpgsqlConnection(configured);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = databaseName, Pooling = true };
        testConnectionString = builder.ConnectionString;
        services = new ServiceCollection().AddCoffeeShopPostgres(testConnectionString)
            .AddSingleton<IClock, SystemClock>().AddSingleton<ICurrentUser>(new CancellationActor(actorId))
            .AddScoped<OrderConfirmationCommandHandler>().AddScoped<DeliveryCommandHandler>().BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        await context.Database.MigrateAsync();
        context.Users.Add(new User(actorId, "cancellation_actor", "cancel@test.local", "hash", "Cashier", null, Now, Now));
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (services is not null) await services.DisposeAsync();
        if (testConnectionString is not null)
        {
            await using var pooledConnection = new NpgsqlConnection(testConnectionString);
            NpgsqlConnection.ClearPool(pooledConnection);
        }
        if (adminConnectionString is null) return;
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task Cancel_RestoresOriginalConsumptionAndLoyaltyAndReleasesTableWithAudit()
    {
        var seed = await SeedAsync(channel: "dine_in", loyalty: true);
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            await context.Database.ExecuteSqlRawAsync("UPDATE catalog.recipe_ingredients SET quantity = quantity * 3");
            var ingredient = await context.Ingredients.FirstAsync();
            ingredient.Deactivate(Now);
            await context.SaveChangesAsync();
        }

        var result = await CancelAsync(seed.OrderId, " Customer changed their mind ");
        Assert.True(result.IsSuccess);
        await AssertCancelledAsync(seed, expectedLoyaltyBalance: 100);
        await using var verification = services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var order = await db.Orders.SingleAsync(order => order.Id == seed.OrderId);
        Assert.Equal("Customer changed their mind", order.CancelReason);
        var history = await db.OrderStatusHistories.SingleAsync(history => history.OrderId == seed.OrderId && history.ToStatus == OrderStatus.Cancelled);
        Assert.Equal(actorId, history.ChangedBy);
        Assert.Equal(order.CancelledAt, history.ChangedAt);
        Assert.All(await db.StockMovements.Where(movement => movement.Type == StockMovementType.Restoration).ToListAsync(),
            movement => { Assert.Equal(actorId, movement.ActorId); Assert.Equal(order.CancelReason, movement.Reason); });
        var reversals = await db.LoyaltyTransactions.Where(transaction => transaction.ReversesTransactionId != null).ToListAsync();
        Assert.Equal(2, reversals.Count);
        Assert.All(reversals, transaction => Assert.Equal(actorId, transaction.CreatedBy));
    }

    [PostgresFact]
    public async Task Cancel_RetriesInSameAndFreshScopes_PreserveFirstCancellationAndEffects()
    {
        var seed = await SeedAsync(loyalty: true);
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<OrderCancellationCommandHandler>();
        var first = await handler.CancelAsync(new(seed.OrderId, "Original reason"));
        Assert.True(first.IsSuccess);
        UtcTimestamp? originalCancellation;
        await using (var original = services.CreateAsyncScope())
        {
            originalCancellation = (await original.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Orders.SingleAsync()).CancelledAt;
        }
        Assert.Equal(ErrorType.Conflict, (await handler.CancelAsync(new(seed.OrderId, "Retry reason"))).Error!.Type);
        Assert.Equal(ErrorType.Conflict, (await CancelAsync(seed.OrderId, "Another retry")).Error!.Type);
        await AssertCancelledAsync(seed, expectedLoyaltyBalance: 100);
        await using var verify = services.CreateAsyncScope();
        var order = await verify.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Orders.SingleAsync();
        Assert.Equal("Original reason", order.CancelReason);
        Assert.Equal(originalCancellation, order.CancelledAt);
    }

    [PostgresFact]
    public async Task Cancel_ConcurrentRequestsFromIndependentContexts_ApplyEffectsOnce()
    {
        var seed = await SeedAsync(channel: "dine_in", loyalty: true);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, 8).Select(async index =>
        {
            await gate.Task;
            return await CancelAsync(seed.OrderId, $"Concurrent request {index}");
        }).ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(results, result => result.IsSuccess);
        Assert.All(results.Where(result => result.IsFailure), result => Assert.Equal(ErrorType.Conflict, result.Error!.Type));
        await AssertCancelledAsync(seed, expectedLoyaltyBalance: 100);
    }

    [PostgresFact]
    public async Task Restore_ConcurrentRetriesAndLaterCancellation_DoNotRestoreTwice()
    {
        var seed = await SeedAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var scope = services.CreateAsyncScope();
            await gate.Task;
            return await scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>()
                .RestoreOrderInventoryAsync(new(seed.OrderId, "Cancellation recovery", actorId));
        }).ToArray();
        gate.SetResult();
        Assert.All(await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(30)), result => Assert.True(result.IsSuccess));
        Assert.True((await CancelAsync(seed.OrderId)).IsSuccess);
        await AssertCancelledAsync(seed);
    }

    [PostgresFact]
    public async Task Cancel_DifferentOrdersSharingIngredients_RestoresBothWithoutLostUpdates()
    {
        var seed = await SeedAsync();
        EntityId secondId;
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            var item = await context.MenuItems.SingleAsync();
            var variant = await context.SizeVariants.SingleAsync();
            var order = new Order(EntityId.New(), "SECOND", "takeaway", actorId, Now,
                [new OrderLine(EntityId.New(), item, variant, 1)]);
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            secondId = order.Id;
            Assert.True((await scope.ServiceProvider.GetRequiredService<OrderConfirmationCommandHandler>()
                .ConfirmAsync(new(secondId, actorId))).IsSuccess);
        }

        var results = await Task.WhenAll(CancelAsync(seed.OrderId), CancelAsync(secondId)).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.All(results, result => Assert.True(result.IsSuccess));
        await AssertCancelledAsync(seed);
        await using var verify = services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        Assert.Equal(2, await db.OrderInventoryRestorations.CountAsync());
        Assert.Equal(4, await db.StockMovements.CountAsync(movement => movement.Type == StockMovementType.Restoration));
    }

    [PostgresFact]
    public async Task Cancel_LoyaltyConflict_RollsBackSavedStockAndState_ThenAllowsSameScopeRetry()
    {
        var seed = await SeedAsync(channel: "dine_in", spentEarnedPoints: true);
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<OrderCancellationCommandHandler>();
        var result = await handler.CancelAsync(new(seed.OrderId, "Reason"));
        Assert.Equal("loyalty.reversal_balance_conflict", result.Error!.Code);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().ChangeTracker.Entries());
        await AssertUnchangedAsync(seed);

        await using (var replenish = services.CreateAsyncScope())
        {
            var context = replenish.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            var customer = await context.Customers.SingleAsync();
            customer.EarnLoyaltyPoints(EntityId.New(), 20, Now);
            await context.SaveChangesAsync();
        }

        Assert.True((await handler.CancelAsync(new(seed.OrderId, "Reason"))).IsSuccess);
        await AssertCancelledAsync(seed, expectedLoyaltyBalance: 0);
    }

    [PostgresFact]
    public async Task Cancel_FailureAfterLoyaltySave_RollsBackEveryEffectAndAllowsRetry()
    {
        var seed = await SeedAsync(channel: "dine_in", loyalty: true);
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var context = provider.GetRequiredService<CoffeeShopDbContext>();
        var failingUnitOfWork = new FailThirdSave(context);
        var transactions = provider.GetRequiredService<ITransactionManager>();
        var clock = provider.GetRequiredService<IClock>();
        var user = provider.GetRequiredService<ICurrentUser>();
        var handler = new OrderCancellationCommandHandler(provider.GetRequiredService<IOrderCancellationRepository>(),
            new InventoryCommandHandler(provider.GetRequiredService<IInventoryRepository>(), failingUnitOfWork, transactions, clock),
            new LoyaltyCommandHandler(provider.GetRequiredService<ICustomerRepository>(), failingUnitOfWork, transactions, clock, user),
            failingUnitOfWork, transactions, clock, user);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.CancelAsync(new(seed.OrderId, "Reason")));
        Assert.Empty(context.ChangeTracker.Entries());
        await AssertUnchangedAsync(seed);
        Assert.True((await provider.GetRequiredService<OrderCancellationCommandHandler>().CancelAsync(new(seed.OrderId, "Retry"))).IsSuccess);
        await AssertCancelledAsync(seed, expectedLoyaltyBalance: 100);
    }

    [PostgresFact]
    public async Task Cancel_WalkInDraftWithoutConsumption_SucceedsWithoutStockMovements()
    {
        var seed = await SeedAsync(confirm: false);
        Assert.True((await CancelAsync(seed.OrderId)).IsSuccess);
        await AssertCancelledAsync(seed, consumed: false);
    }

    [PostgresFact]
    public async Task Cancel_RedeemedDraft_ReturnsPointsThroughTheApplicationWorkflow()
    {
        var seed = await SeedAsync(confirm: false, attachCustomer: true);
        await using (var scope = services.CreateAsyncScope())
        {
            var redeemed = await scope.ServiceProvider.GetRequiredService<LoyaltyCommandHandler>()
                .RedeemForOrderAsync(new(seed.OrderId, 20));
            Assert.True(redeemed.IsSuccess);
        }

        Assert.True((await CancelAsync(seed.OrderId)).IsSuccess);
        await AssertCancelledAsync(seed, expectedLoyaltyBalance: 100, consumed: false);
    }

    [PostgresFact]
    public async Task Cancel_HoldsOrderLock_ConcurrentDeliveryReloadsAndRejectsCancelledOrder()
    {
        var seed = await SeedAsync(channel: "delivery");
        await using (var prepare = services.CreateAsyncScope())
        {
            var db = prepare.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            (await db.Deliveries.SingleAsync()).Assign(actorId, actorId, Now);
            await db.SaveChangesAsync();
        }

        await using var cancellingScope = services.CreateAsyncScope();
        var cancellingDb = cancellingScope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        await using var transaction = await cancellingDb.Database.BeginTransactionAsync();
        await cancellingScope.ServiceProvider.GetRequiredService<IOrderCancellationRepository>()
            .LoadOrderForCancellationAsync(seed.OrderId);

        await using var deliveryScope = services.CreateAsyncScope();
        var provider = deliveryScope.ServiceProvider;
        var deliveryDb = provider.GetRequiredService<CoffeeShopDbContext>();
        // Simulate a client scope that read the old state before cancellation obtained its lock.
        await deliveryDb.Orders.SingleAsync();
        await deliveryDb.Deliveries.SingleAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deliveryHandler = new DeliveryCommandHandler(
            new SignallingDeliveryRepository(provider.GetRequiredService<IDeliveryRepository>(), entered),
            deliveryDb, provider.GetRequiredService<IClock>(), provider.GetRequiredService<ITransactionManager>());
        var deliveryAttempt = deliveryHandler.StartAsync(new(seed.OrderId, actorId));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True((await cancellingScope.ServiceProvider.GetRequiredService<OrderCancellationCommandHandler>()
            .CancelAsync(new(seed.OrderId, "Cancelled before dispatch"))).IsSuccess);
        await transaction.CommitAsync();
        Assert.Equal(ErrorType.Conflict, (await deliveryAttempt.WaitAsync(TimeSpan.FromSeconds(10))).Error!.Type);
        await using var verify = services.CreateAsyncScope();
        var verifiedDb = verify.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        Assert.Equal(DeliveryStatus.Assigned, (await verifiedDb.Deliveries.SingleAsync()).Status);
        Assert.Equal(1, await verifiedDb.DeliveryStatusHistories.CountAsync());
        await AssertCancelledAsync(seed);
    }

    [PostgresFact]
    public async Task DeliverySchemaUpgrade_AppliesMissingHistoryAfterAnAlreadyAppliedNewerMigration()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        // Reproduce a database upgraded before the old history migration was discoverable.
        await db.Database.ExecuteSqlRawAsync("DROP TABLE sales.delivery_status_history");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" IN ('20261010060000_AddDeliveryStatusHistory', '20261015021700_RepairDeliveryFulfillmentSchema')");
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(0, await db.DeliveryStatusHistories.CountAsync());
        var seed = await SeedAsync(channel: "delivery");
        var delivery = await db.Deliveries.SingleAsync();
        delivery.Assign(actorId, actorId, Now);
        delivery.StartDelivery(actorId, Now);
        await db.SaveChangesAsync();
        Assert.True((await CancelAsync(seed.OrderId)).IsSuccess);
    }

    [PostgresTheory]
    [InlineData("assign")]
    [InlineData("start")]
    [InlineData("complete")]
    [InlineData("fail")]
    public async Task Cancel_PreventsFurtherDeliveryWork(string operation)
    {
        var seed = await SeedAsync(channel: "delivery");
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var delivery = await db.Deliveries.SingleAsync();
        if (operation != "assign") delivery.Assign(actorId, actorId, Now);
        if (operation is "complete" or "fail") delivery.StartDelivery(actorId, Now);
        await db.SaveChangesAsync();
        var historyCount = await db.DeliveryStatusHistories.CountAsync();
        Assert.True((await CancelAsync(seed.OrderId)).IsSuccess);
        // This scope deliberately still tracks the pre-cancellation order and delivery.
        var handler = scope.ServiceProvider.GetRequiredService<DeliveryCommandHandler>();
        var result = operation switch
        {
            "assign" => await handler.AssignAsync(new(seed.OrderId, actorId, actorId)),
            "start" => await handler.StartAsync(new(seed.OrderId, actorId)),
            "complete" => await handler.CompleteAsync(new(seed.OrderId, actorId)),
            _ => await handler.FailAsync(new(seed.OrderId, "Failed", actorId))
        };
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(historyCount, await db.DeliveryStatusHistories.CountAsync());
    }

    [PostgresFact]
    public async Task RestorationGuard_RejectsDuplicateOrderAtDatabaseBoundary()
    {
        var seed = await SeedAsync();
        Assert.True((await CancelAsync(seed.OrderId)).IsSuccess);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        context.OrderInventoryRestorations.Add(new(seed.OrderId, Now, actorId));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        await AssertCancelledAsync(seed);
    }

    private async Task<Result<OrderCancellationResult>> CancelAsync(EntityId orderId, string reason = "Customer request")
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OrderCancellationCommandHandler>().CancelAsync(new(orderId, reason));
    }

    private async Task<Seed> SeedAsync(string channel = "takeaway", bool loyalty = false, bool spentEarnedPoints = false,
        bool confirm = true, bool attachCustomer = false)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var category = new Category(EntityId.New(), "Coffee", Now, Now);
        var item = new MenuItem(EntityId.New(), category, "Latte", Now, Now);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(100m));
        var beans = new Ingredient(EntityId.New(), "Beans", "g", Now, Now, availableQuantity: 100m);
        var milk = new Ingredient(EntityId.New(), "Milk", "ml", Now, Now, availableQuantity: 50m);
        var customer = loyalty || spentEarnedPoints || attachCustomer
            ? new Customer(EntityId.New(), "Alice", "0901234567", Now, Now, loyaltyPointsBalance: 100) : null;
        var table = channel == "dine_in" ? new DiningTable(EntityId.New(), "T1") : null;
        var orderId = EntityId.New();
        var delivery = channel == "delivery" ? Delivery.CreateOrderSpecific(EntityId.New(), orderId, "Alice", "0901234567", "123 Main Street") : null;
        var order = new Order(orderId, $"ORD-{orderId.Value:N}"[..20], channel, actorId, Now,
            [new OrderLine(EntityId.New(), item, variant, 1)], customerId: customer?.Id, tableId: table?.Id, delivery: delivery);
        context.AddRange(category, item, variant, beans, milk);
        if (customer is not null)
        {
            context.Customers.Add(customer);
            context.LoyaltySettings.Add(new LoyaltySettings(EntityId.New(), 0.1m, 1m, 20m, Now));
        }
        if (table is not null) context.DiningTables.Add(table);
        context.Orders.Add(order);
        if (delivery is not null) context.Deliveries.Add(delivery);
        context.RecipeIngredients.AddRange(new RecipeIngredient(EntityId.New(), variant, beans, 4m, "g"),
            new RecipeIngredient(EntityId.New(), variant, milk, 3m, "ml"));
        if (loyalty)
        {
            // Pre-existing ledger entries must be compensated even on imported orders.
            customer!.EarnLoyaltyPoints(EntityId.New(), 10, Now, order.Id, actorId);
            customer.RedeemLoyaltyPoints(EntityId.New(), 20, Now, order.Id, actorId);
            order.ApplyLoyaltyRedemption(20, new Money(20m));
        }
        if (spentEarnedPoints)
        {
            customer!.EarnLoyaltyPoints(EntityId.New(), 20, Now, order.Id, actorId);
            customer.RedeemLoyaltyPoints(EntityId.New(), 120, Now, createdBy: actorId);
        }

        await context.SaveChangesAsync();
        if (confirm)
        {
            var result = await scope.ServiceProvider.GetRequiredService<OrderConfirmationCommandHandler>().ConfirmAsync(new(order.Id, actorId));
            Assert.True(result.IsSuccess, result.Error?.Description);
        }

        return new(order.Id, beans.Id, milk.Id, table?.Id, customer?.Id);
    }

    private async Task AssertCancelledAsync(Seed seed, int? expectedLoyaltyBalance = null, bool consumed = true)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        Assert.Equal(OrderStatus.Cancelled, (await db.Orders.SingleAsync(order => order.Id == seed.OrderId)).Status);
        Assert.Equal(100m, (await db.Ingredients.SingleAsync(ingredient => ingredient.Id == seed.BeansId)).AvailableQuantity);
        Assert.Equal(50m, (await db.Ingredients.SingleAsync(ingredient => ingredient.Id == seed.MilkId)).AvailableQuantity);
        Assert.Equal(consumed ? 2 : 0, await db.StockMovements.CountAsync(movement => movement.OrderId == seed.OrderId && movement.Type == StockMovementType.Restoration));
        Assert.Equal(1, await db.OrderInventoryRestorations.CountAsync(restoration => restoration.OrderId == seed.OrderId));
        Assert.Equal(1, await db.OrderStatusHistories.CountAsync(history => history.OrderId == seed.OrderId && history.ToStatus == OrderStatus.Cancelled));
        if (seed.TableId is not null)
        {
            var table = await db.DiningTables.SingleAsync(table => table.Id == seed.TableId);
            Assert.False(table.IsOccupied);
            Assert.Null(table.ActiveOrderId);
        }
        if (expectedLoyaltyBalance is not null)
        {
            Assert.Equal(expectedLoyaltyBalance, (await db.Customers.SingleAsync(customer => customer.Id == seed.CustomerId)).LoyaltyPointsBalance);
            var transactions = await db.LoyaltyTransactions.Where(transaction => transaction.OrderId == seed.OrderId).ToListAsync();
            var originals = transactions.Where(transaction => transaction.ReversesTransactionId is null).ToArray();
            Assert.Equal(originals.Length * 2, transactions.Count);
            Assert.All(originals, original => Assert.Single(transactions, reversal => reversal.ReversesTransactionId == original.Id));
        }
    }

    private async Task AssertUnchangedAsync(Seed seed)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var order = await db.Orders.SingleAsync(order => order.Id == seed.OrderId);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Null(order.CancelledAt);
        Assert.Null(order.CancelReason);
        Assert.Equal(96m, (await db.Ingredients.SingleAsync(ingredient => ingredient.Id == seed.BeansId)).AvailableQuantity);
        Assert.Equal(47m, (await db.Ingredients.SingleAsync(ingredient => ingredient.Id == seed.MilkId)).AvailableQuantity);
        Assert.False(await db.OrderInventoryRestorations.AnyAsync());
        Assert.False(await db.StockMovements.AnyAsync(movement => movement.Type == StockMovementType.Restoration));
        Assert.False(await db.OrderStatusHistories.AnyAsync(history => history.ToStatus == OrderStatus.Cancelled));
        Assert.False(await db.LoyaltyTransactions.AnyAsync(transaction => transaction.ReversesTransactionId != null));
        if (seed.TableId is not null)
        {
            var table = await db.DiningTables.SingleAsync();
            Assert.True(table.IsOccupied);
            Assert.Equal(order.Id, table.ActiveOrderId);
        }
    }

    private sealed record Seed(EntityId OrderId, EntityId BeansId, EntityId MilkId, EntityId? TableId, EntityId? CustomerId);

    private sealed class FailThirdSave(CoffeeShopDbContext context) : IUnitOfWork
    {
        private int count;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => ++count == 3
            ? throw new InvalidOperationException("Injected failure after inventory and loyalty saves.")
            : context.SaveChangesAsync(cancellationToken);
    }

    private sealed class SignallingDeliveryRepository(IDeliveryRepository inner, TaskCompletionSource entered) : IDeliveryRepository
    {
        public Task<Order?> LoadOrderForUpdateAsync(EntityId orderId, CancellationToken cancellationToken = default)
        {
            entered.SetResult();
            return inner.LoadOrderForUpdateAsync(orderId, cancellationToken);
        }

        public Task<Delivery?> FindDeliveryForOrderAsync(EntityId orderId, CancellationToken cancellationToken = default) =>
            inner.FindDeliveryForOrderAsync(orderId, cancellationToken);
        public Task<bool> IsEligibleDeliveryStaffAsync(EntityId staffId, CancellationToken cancellationToken = default) =>
            inner.IsEligibleDeliveryStaffAsync(staffId, cancellationToken);
        public void AddStatusHistory(DeliveryStatusHistory history) => inner.AddStatusHistory(history);
    }

    private sealed class CancellationActor(EntityId id) : ICurrentUser
    {
        public EntityId? Id => id;
        public string? Username => "cancellation_actor";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> Permissions => ["orders.cancel"];
        public bool HasPermission(string permission) => Permissions.Contains(permission);
        public bool IsInRole(string role) => false;
        public EntityId GetRequiredId() => id;
    }
}
