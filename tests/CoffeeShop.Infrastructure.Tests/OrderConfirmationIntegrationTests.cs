namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

/// <summary>
/// Integration tests proving that order confirmation is all-or-nothing:
/// success deducts every required ingredient once, and any shortage leaves
/// the order, balances, and movement ledger unchanged. Requires PostgreSQL
/// reachable at COFFEESHOP_TEST_POSTGRES; tests skip otherwise.
/// </summary>
public sealed class OrderConfirmationIntegrationTests : IAsyncLifetime
{
    public const string ConnectionVariable = CatalogPersistenceIntegrationTests.ConnectionVariable;
    private readonly string databaseName = $"coffeeshop_confirm_{Guid.NewGuid():N}";
    private ServiceProvider services = null!;
    private string adminConnectionString = null!;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionVariable);
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
        services = new ServiceCollection().AddCoffeeShopPostgres(builder.ConnectionString)
            .AddSingleton<IClock, SystemClock>().AddScoped<OrderConfirmationCommandHandler>().BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Database.MigrateAsync();
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
    public async Task Confirm_SufficientStock_DeductsIngredientsAndPersistsConfirmation()
    {
        var seed = await SeedAsync(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 2);
        var handler = Handler();

        var result = await handler.ConfirmAsync(new(seed.OrderId, seed.ActorId));

        Assert.True(result.IsSuccess);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var order = await context.Orders.Include(order => order.Lines).ThenInclude(line => line.Addons)
            .FirstAsync(order => order.Id == seed.OrderId);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.NotNull(order.ConfirmedAt);
        Assert.Equal(1, await context.OrderStatusHistories.CountAsync(history =>
            history.OrderId == seed.OrderId && history.ToStatus == OrderStatus.Confirmed));
        var ingredient = await context.Ingredients.SingleAsync(i => i.Id == seed.IngredientId);
        Assert.Equal(64m, ingredient.AvailableQuantity); // 100 - 18 * 2
        var movement = await context.StockMovements.SingleAsync(m => m.OrderId == seed.OrderId);
        Assert.Equal(StockMovementType.OrderConsumption, movement.Type);
        Assert.Equal(-36m, movement.QuantityDelta);
        Assert.Equal(seed.IngredientId, movement.IngredientId);
    }

    [PostgresFact]
    public async Task Confirm_Shortage_RollsBackEverything()
    {
        var seed = await SeedAsync(ingredientStock: 30m, recipeQuantity: 18m, lineQuantity: 2);
        var handler = Handler();

        var result = await handler.ConfirmAsync(new(seed.OrderId, seed.ActorId));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.BusinessRule, result.Error!.Type);
        Assert.Equal("orders.insufficient_stock", result.Error.Code);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var order = await context.Orders.FirstAsync(order => order.Id == seed.OrderId);
        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Null(order.ConfirmedAt);
        Assert.Empty(await context.OrderStatusHistories.Where(history => history.OrderId == seed.OrderId).ToListAsync());
        var ingredient = await context.Ingredients.SingleAsync(i => i.Id == seed.IngredientId);
        Assert.Equal(30m, ingredient.AvailableQuantity);
        Assert.Empty(await context.StockMovements.Where(m => m.OrderId == seed.OrderId).ToListAsync());
    }

    [PostgresFact]
    public async Task Confirm_MultiIngredientShortage_DeductsNothingFromAnyIngredient()
    {
        var seed = await SeedAsync(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var milk = new Ingredient(EntityId.New(), "Milk", "ml", Timestamp, Timestamp, availableQuantity: 10m);
        context.Ingredients.Add(milk);
        await context.SaveChangesAsync();
        var variant = await context.SizeVariants.SingleAsync(v => v.Id == seed.SizeVariantId);
        context.RecipeIngredients.Add(new RecipeIngredient(EntityId.New(), variant, milk, 25m, "ml"));
        await context.SaveChangesAsync();
        var handler = Handler();

        var result = await handler.ConfirmAsync(new(seed.OrderId, seed.ActorId));

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error!.Details, detail => detail.Message.Contains("Milk"));
        var fresh = services.CreateAsyncScope().ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        Assert.Equal(100m, (await fresh.Ingredients.SingleAsync(i => i.Id == seed.IngredientId)).AvailableQuantity);
        Assert.Equal(10m, (await fresh.Ingredients.SingleAsync(i => i.Id == milk.Id)).AvailableQuantity);
        Assert.Empty(await fresh.StockMovements.Where(m => m.OrderId == seed.OrderId).ToListAsync());
    }

    [PostgresFact]
    public async Task Confirm_DineInOrder_OccupiesTableAndPersistsIt()
    {
        var seed = await SeedAsync(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1, channel: "dine_in");
        var handler = Handler();

        var result = await handler.ConfirmAsync(new(seed.OrderId, seed.ActorId));

        Assert.True(result.IsSuccess);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var table = await context.DiningTables.SingleAsync(t => t.Id == seed.TableId);
        Assert.True(table.IsOccupied);
        Assert.Equal(seed.OrderId, table.ActiveOrderId);
    }

    [PostgresFact]
    public async Task Confirm_OccupiedTable_LeavesOrderDraftAndStockUntouched()
    {
        var seed = await SeedAsync(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1, channel: "dine_in");
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        // A blocker order must exist so the table's active-order foreign key is satisfied.
        var blockerId = EntityId.New();
        context.Orders.Add(new Order(
            blockerId, $"ORD-{blockerId.Value:N}"[..12], "dine_in", seed.ActorId, Timestamp,
            [new OrderLine(EntityId.New(),
                await context.MenuItems.FirstAsync(),
                await context.SizeVariants.FirstAsync(), 1)],
            tableId: seed.TableId));
        await context.SaveChangesAsync();
        var table = await context.DiningTables.SingleAsync(t => t.Id == seed.TableId);
        table.Occupy(blockerId, partySize: 1);
        await context.SaveChangesAsync();
        var handler = Handler();

        var result = await handler.ConfirmAsync(new(seed.OrderId, seed.ActorId));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        var fresh = services.CreateAsyncScope().ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        Assert.Equal(OrderStatus.Draft, (await fresh.Orders.FirstAsync(o => o.Id == seed.OrderId)).Status);
        Assert.Equal(100m, (await fresh.Ingredients.SingleAsync(i => i.Id == seed.IngredientId)).AvailableQuantity);
        Assert.Empty(await fresh.StockMovements.Where(m => m.OrderId == seed.OrderId).ToListAsync());
    }

    [PostgresFact]
    public async Task Confirm_RepeatedConfirmation_DoesNotDeductTwice()
    {
        var seed = await SeedAsync(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1);
        var handler = Handler();
        Assert.True((await handler.ConfirmAsync(new(seed.OrderId, seed.ActorId))).IsSuccess);

        var retry = await handler.ConfirmAsync(new(seed.OrderId, seed.ActorId));

        Assert.True(retry.IsSuccess);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        Assert.Equal(82m, (await context.Ingredients.SingleAsync(i => i.Id == seed.IngredientId)).AvailableQuantity);
        Assert.Equal(1, await context.StockMovements.CountAsync(m => m.OrderId == seed.OrderId));
    }

    private OrderConfirmationCommandHandler Handler()
    {
        var scope = services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<OrderConfirmationCommandHandler>();
    }

    private static UtcTimestamp Timestamp { get; } = new(DateTimeOffset.UtcNow);

    private async Task<Seed> SeedAsync(
        decimal ingredientStock,
        decimal recipeQuantity,
        int lineQuantity,
        string channel = "takeaway")
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var catalog = new CatalogCommandHandler(
            provider.GetRequiredService<ICatalogRepository>(), provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<ITransactionManager>(), new SystemClock());
        var recipes = new RecipeCommandHandler(
            provider.GetRequiredService<ICatalogRepository>(), provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<ITransactionManager>());

        var category = (await catalog.CreateCategoryAsync(new("Coffee", 2))).Value;
        var item = (await catalog.CreateMenuItemAsync(new(category, "Latte", null, [new("Large", 45_000m)]))).Value;
        var variant = Assert.Single(await provider.GetRequiredService<ICatalogRepository>().ListSizeVariantsAsync(item));
        var ingredient = new Ingredient(
            EntityId.New(), "Coffee beans", "g", Timestamp, Timestamp, availableQuantity: ingredientStock);
        var context = provider.GetRequiredService<CoffeeShopDbContext>();
        context.Ingredients.Add(ingredient);
        await context.SaveChangesAsync();
        Assert.True((await recipes.ReplaceRecipeAsync(new(variant.Id, [new(ingredient.Id, recipeQuantity, "g")]))).IsSuccess);

        var tableId = channel == "dine_in" ? EntityId.New() : null;
        if (tableId is not null)
        {
            context.DiningTables.Add(new DiningTable(tableId, "T1"));
            await context.SaveChangesAsync();
        }

        var createdBy = EntityId.New();
        context.Users.Add(new User(
            createdBy, $"cashier-{createdBy.Value:N}"[..20], "cashier@test.local", "hash", "Cashier", null,
            Timestamp, Timestamp));
        await context.SaveChangesAsync();

        var orderId = EntityId.New();
        var order = new Order(
            orderId, $"ORD-{orderId.Value:N}"[..12], channel, createdBy, Timestamp,
            [new OrderLine(
                EntityId.New(),
                await context.MenuItems.SingleAsync(i => i.Id == item),
                await context.SizeVariants.SingleAsync(v => v.Id == variant.Id),
                lineQuantity)],
            tableId: tableId);
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return new(order.Id, ingredient.Id, variant.Id, tableId, createdBy);
    }

    private sealed record Seed(EntityId OrderId, EntityId IngredientId, EntityId SizeVariantId, EntityId? TableId, EntityId ActorId);
}
