namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Integration tests proving that posting a purchase receipt increases
/// ingredient stock exactly once. Requires a local PostgreSQL reachable at
/// the test connection string; all tests skip gracefully otherwise.
/// </summary>
[Collection("PostgreSQL integration")]
public sealed class ReceiptPostingIntegrationTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=coffeeshop_receipt_test;Username=test;Password=test";

    private ServiceProvider? serviceProvider;
    private bool databaseAvailable;

    public async Task InitializeAsync()
    {
        databaseAvailable = await TryProbeDatabaseAsync();

        if (!databaseAvailable)
        {
            return;
        }

        var services = new ServiceCollection();
        services.AddCoffeeShopPostgres(ConnectionString);
        serviceProvider = services.BuildServiceProvider();

        // Rebuild the database from the migrations for a clean state.
        await using var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (serviceProvider is not null)
        {
            await serviceProvider.DisposeAsync();
        }
    }

    private InventoryCommandHandler CreateHandler()
    {
        var scope = serviceProvider!.CreateScope();
        return new InventoryCommandHandler(
            scope.ServiceProvider.GetRequiredService<IInventoryRepository>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            scope.ServiceProvider.GetRequiredService<ITransactionManager>(),
            new FixedClock());
    }

    [Fact]
    public async Task PostingReceipt_IncreasesStockExactlyOnce()
    {
        if (!databaseAvailable) { return; }

        var handler = CreateHandler();
        var ingredientId = await SeedIngredientAsync(handler, availableQuantity: 10m);
        var receiptId = await SeedDraftReceiptAsync(handler, ingredientId, lines: [(5m, 30m), (3m, 28m)]);

        var result = await handler.PostReceiptAsync(new PostReceiptCommand(receiptId, EntityId.New()));

        Assert.True(result.IsSuccess);
        var (receipt, ingredient) = await LoadReceiptAndIngredientAsync(ingredientId, receiptId);
        Assert.Equal(PurchaseReceiptStatus.Posted, receipt.Status);
        Assert.NotNull(receipt.PostedAt);
        Assert.Equal(18m, ingredient.AvailableQuantity);
        var movements = await LoadMovementsAsync(receiptId);
        Assert.Equal(2, movements.Count);
        Assert.All(movements, movement =>
        {
            Assert.Equal(StockMovementType.Receipt, movement.Type);
            Assert.Equal(receiptId, movement.PurchaseReceiptId);
        });
    }

    [Fact]
    public async Task PostingReceiptTwice_SecondAttemptFailsWithoutDoubleIncrease()
    {
        if (!databaseAvailable) { return; }

        var handler = CreateHandler();
        var ingredientId = await SeedIngredientAsync(handler, availableQuantity: 10m);
        var receiptId = await SeedDraftReceiptAsync(handler, ingredientId, lines: [(5m, 30m)]);

        var first = await handler.PostReceiptAsync(new PostReceiptCommand(receiptId, EntityId.New()));
        Assert.True(first.IsSuccess);

        var second = await handler.PostReceiptAsync(new PostReceiptCommand(receiptId, EntityId.New()));

        Assert.True(second.IsFailure);
        Assert.Equal(ErrorType.Conflict, second.Error!.Type);
        var (_, ingredient) = await LoadReceiptAndIngredientAsync(ingredientId, receiptId);
        Assert.Equal(15m, ingredient.AvailableQuantity);
    }

    [Fact]
    public async Task PostingReceiptWithUnknownIngredient_PersistsNothing()
    {
        if (!databaseAvailable) { return; }

        var handler = CreateHandler();
        var ingredientId = await SeedIngredientAsync(handler, availableQuantity: 10m);
        var receiptId = await SeedDraftReceiptAsync(handler, ingredientId, lines: [(5m, 30m)]);
        // Add a line referencing an ingredient that does not exist.
        var addUnknown = await handler.AddReceiptLineAsync(new AddReceiptLineCommand(receiptId, EntityId.New(), 2m, 20m));
        Assert.True(addUnknown.IsSuccess);

        var result = await handler.PostReceiptAsync(new PostReceiptCommand(receiptId, EntityId.New()));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        var (receipt, ingredient) = await LoadReceiptAndIngredientAsync(ingredientId, receiptId);
        Assert.Equal(PurchaseReceiptStatus.Draft, receipt.Status);
        Assert.Equal(10m, ingredient.AvailableQuantity);
    }

    [Fact]
    public async Task ReceiveDelivery_PostsAllLinesOnce_AndRejectsIncompleteOrConflictingDeliveries()
    {
        if (!databaseAvailable) { return; }

        var handler = CreateHandler();
        var ingredientId = await SeedIngredientAsync(handler, availableQuantity: 10m);
        var supplierId = (await handler.CreateSupplierAsync(new("Bean Roasters", "0901234567"))).Value;
        var command = new ReceiveDeliveryCommand(supplierId, "DELIVERY-001",
            [new(ingredientId, 5m, 30m), new(ingredientId, 3m, 28m)], EntityId.New());

        var invalid = await handler.ReceiveDeliveryAsync(command with
        {
            Lines = [command.Lines[0], new(EntityId.New(), 3m, 28m)]
        });
        Assert.True(invalid.IsFailure);
        await using (var scope = serviceProvider!.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
            Assert.Empty(await repository.ListReceiptsAsync());
            Assert.Equal(10m, (await repository.FindIngredientAsync(ingredientId))!.AvailableQuantity);
        }

        var posts = await Task.WhenAll(
            handler.ReceiveDeliveryAsync(command),
            CreateHandler().ReceiveDeliveryAsync(command with { Lines = command.Lines.Reverse().ToArray() }));
        Assert.All(posts, post => Assert.True(post.IsSuccess));
        var receiptId = posts[0].Value.Id;
        Assert.Equal(receiptId, posts[1].Value.Id);

        var conflict = await CreateHandler().ReceiveDeliveryAsync(command with { Lines = [command.Lines[0]] });
        Assert.Equal(ErrorType.Conflict, conflict.Error!.Type);
        var (receipt, ingredient) = await LoadReceiptAndIngredientAsync(ingredientId, receiptId);
        Assert.Equal(PurchaseReceiptStatus.Posted, receipt.Status);
        Assert.Equal(18m, ingredient.AvailableQuantity);
        Assert.Equal(2, (await LoadMovementsAsync(receiptId)).Count);
    }

    private async Task<EntityId> SeedIngredientAsync(InventoryCommandHandler handler, decimal availableQuantity)
    {
        var ingredient = new Ingredient(
            EntityId.New(),
            "Coffee beans",
            "g",
            new UtcTimestamp(DateTimeOffset.UtcNow),
            new UtcTimestamp(DateTimeOffset.UtcNow),
            availableQuantity,
            2m);
        var scope = serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        repository.AddIngredient(ingredient);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        return ingredient.Id;
    }

    private async Task<EntityId> SeedDraftReceiptAsync(
        InventoryCommandHandler handler,
        EntityId ingredientId,
        IReadOnlyList<(decimal Quantity, decimal UnitCost)> lines)
    {
        var supplierId = (await handler.CreateSupplierAsync(new CreateSupplierCommand("Bean Roasters", "0901234567"))).Value;
        var receiptId = (await handler.CreateDraftReceiptAsync(new CreateDraftReceiptCommand(supplierId, $"PR-{Guid.NewGuid():N}"[..20]))).Value;

        foreach (var (quantity, unitCost) in lines)
        {
            var added = await handler.AddReceiptLineAsync(new AddReceiptLineCommand(receiptId, ingredientId, quantity, unitCost));
            Assert.True(added.IsSuccess);
        }

        return receiptId;
    }

    private async Task<(PurchaseReceipt Receipt, Ingredient Ingredient)> LoadReceiptAndIngredientAsync(EntityId ingredientId, EntityId receiptId)
    {
        var scope = serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        var receipt = (await repository.FindReceiptWithLinesAsync(receiptId))!;
        var ingredient = (await repository.FindIngredientAsync(ingredientId))!;
        return (receipt, ingredient);
    }

    private async Task<List<StockMovement>> LoadMovementsAsync(EntityId receiptId)
    {
        await using var scope = serviceProvider!.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        return await context.StockMovements
            .Where(movement => movement.PurchaseReceiptId == receiptId)
            .ToListAsync();
    }

    private static async Task<bool> TryProbeDatabaseAsync()
    {
        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<CoffeeShopDbContext>();
            CoffeeShopPostgresOptions.Configure(optionsBuilder, ConnectionString);
            await using var context = new CoffeeShopDbContext(optionsBuilder.Options);
            return await context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private sealed class FixedClock : CoffeeShop.Application.Common.Time.IClock
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

        public UtcTimestamp UtcNow => new(Now);

        public DateTimeOffset DateTimeUtcNow => Now;
    }
}
