namespace CoffeeShop.Application.Tests.Inventory;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public class InventoryCommandHandlerTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
    private static readonly EntityId ActorId = EntityId.New();

    private readonly FakeInventoryRepository repository = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly TestTransactionManager transactionManager = new();
    private readonly TestClock clock = new(BaseTime);
    private readonly InventoryCommandHandler handler;

    public InventoryCommandHandlerTests()
    {
        handler = new InventoryCommandHandler(repository, unitOfWork, transactionManager, clock);
    }

    private static Ingredient CreateIngredient(decimal availableQuantity = 10m) =>
        new(EntityId.New(), "Coffee beans", "g", new UtcTimestamp(BaseTime), new UtcTimestamp(BaseTime), availableQuantity, 2m);

    private async Task<PurchaseReceipt> CreateDraftReceiptWithLinesAsync(Ingredient ingredient)
    {
        var supplierId = await handler.CreateSupplierAsync(new CreateSupplierCommand("Bean Roasters", "0901234567"));
        Assert.True(supplierId.IsSuccess);

        var receiptId = await handler.CreateDraftReceiptAsync(new CreateDraftReceiptCommand(supplierId.Value, "PR-2026-001"));
        Assert.True(receiptId.IsSuccess);

        var line = await handler.AddReceiptLineAsync(new AddReceiptLineCommand(receiptId.Value, ingredient.Id, 5m, 30m));
        Assert.True(line.IsSuccess);

        return repository.Receipts[receiptId.Value];
    }

    [Fact]
    public async Task CreateSupplierAsync_WithValidData_ReturnsSuccessAndPersists()
    {
        var result = await handler.CreateSupplierAsync(new CreateSupplierCommand("Bean Roasters", "0901234567"));

        Assert.True(result.IsSuccess);
        Assert.Single(repository.Suppliers);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateSupplierAsync_WithInvalidName_ReturnsValidationErrorWithoutPersisting()
    {
        var result = await handler.CreateSupplierAsync(new CreateSupplierCommand(" ", "0901234567"));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Empty(repository.Suppliers);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateSupplierAsync_WithUnknownSupplier_ReturnsNotFound()
    {
        var result = await handler.UpdateSupplierAsync(new UpdateSupplierCommand(EntityId.New(), "X", "0901234567", null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("inventory.supplier_not_found", result.Error.Code);
    }

    [Fact]
    public async Task DeactivateSupplierAsync_WithExistingSupplier_Deactivates()
    {
        var supplierId = (await handler.CreateSupplierAsync(new CreateSupplierCommand("Bean Roasters", "0901234567"))).Value;

        var result = await handler.DeactivateSupplierAsync(new DeactivateSupplierCommand(supplierId));

        Assert.True(result.IsSuccess);
        Assert.False(repository.Suppliers[supplierId].IsActive);
    }

    [Fact]
    public async Task CreateDraftReceiptAsync_WithUnknownSupplier_ReturnsNotFound()
    {
        var result = await handler.CreateDraftReceiptAsync(new CreateDraftReceiptCommand(EntityId.New(), "PR-2026-001"));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("inventory.supplier_not_found", result.Error.Code);
        Assert.Empty(repository.Receipts);
    }

    [Fact]
    public async Task CreateDraftReceiptAsync_WithValidSupplier_CreatesDraft()
    {
        var supplierId = (await handler.CreateSupplierAsync(new CreateSupplierCommand("Bean Roasters", "0901234567"))).Value;

        var result = await handler.CreateDraftReceiptAsync(new CreateDraftReceiptCommand(supplierId, "PR-2026-001"));

        Assert.True(result.IsSuccess);
        var receipt = repository.Receipts[result.Value];
        Assert.Equal(PurchaseReceiptStatus.Draft, receipt.Status);
        Assert.Equal(supplierId, receipt.SupplierId);
    }

    [Fact]
    public async Task AddReceiptLineAsync_WithUnknownReceipt_ReturnsNotFound()
    {
        var result = await handler.AddReceiptLineAsync(new AddReceiptLineCommand(EntityId.New(), EntityId.New(), 5m, 30m));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("inventory.receipt_not_found", result.Error.Code);
    }

    [Fact]
    public async Task AddReceiptLineAsync_WithInvalidQuantity_ReturnsValidationError()
    {
        var ingredient = CreateIngredient();
        repository.AddIngredient(ingredient);
        var receipt = await CreateDraftReceiptWithLinesAsync(ingredient);

        var result = await handler.AddReceiptLineAsync(new AddReceiptLineCommand(receipt.Id, ingredient.Id, 0m, 30m));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Single(receipt.Lines);
    }

    [Fact]
    public async Task RemoveReceiptLineAsync_WithExistingLine_RemovesLine()
    {
        var ingredient = CreateIngredient();
        repository.AddIngredient(ingredient);
        var receipt = await CreateDraftReceiptWithLinesAsync(ingredient);
        var lineId = receipt.Lines.Single().Id;

        var result = await handler.RemoveReceiptLineAsync(new RemoveReceiptLineCommand(receipt.Id, lineId));

        Assert.True(result.IsSuccess);
        Assert.Empty(receipt.Lines);
    }

    [Fact]
    public async Task CancelReceiptAsync_OnDraftReceipt_Cancels()
    {
        var ingredient = CreateIngredient();
        repository.AddIngredient(ingredient);
        var receipt = await CreateDraftReceiptWithLinesAsync(ingredient);

        var result = await handler.CancelReceiptAsync(new CancelReceiptCommand(receipt.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseReceiptStatus.Cancelled, receipt.Status);
    }

    [Fact]
    public async Task PostReceiptAsync_WithValidLines_PostsAndIncreasesBalanceExactlyOnce()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);
        repository.AddIngredient(ingredient);
        var receipt = await CreateDraftReceiptWithLinesAsync(ingredient);
        await handler.AddReceiptLineAsync(new AddReceiptLineCommand(receipt.Id, ingredient.Id, 3m, 28m));

        var result = await handler.PostReceiptAsync(new PostReceiptCommand(receipt.Id, ActorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseReceiptStatus.Posted, receipt.Status);
        Assert.Equal(18m, ingredient.AvailableQuantity);
        Assert.Equal(2, repository.StockMovements.Count);
        Assert.All(repository.StockMovements, movement =>
        {
            Assert.Equal(StockMovementType.Receipt, movement.Type);
            Assert.Equal(receipt.Id, movement.PurchaseReceiptId);
            Assert.Equal(ActorId, movement.ActorId);
        });
    }

    [Fact]
    public async Task PostReceiptAsync_Twice_SecondAttemptFailsWithoutDoubleIncrease()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);
        repository.AddIngredient(ingredient);
        var receipt = await CreateDraftReceiptWithLinesAsync(ingredient);

        var first = await handler.PostReceiptAsync(new PostReceiptCommand(receipt.Id, ActorId));
        Assert.True(first.IsSuccess);
        Assert.Equal(15m, ingredient.AvailableQuantity);

        var second = await handler.PostReceiptAsync(new PostReceiptCommand(receipt.Id, ActorId));

        Assert.True(second.IsFailure);
        Assert.Equal(ErrorType.Conflict, second.Error!.Type);
        Assert.Equal(15m, ingredient.AvailableQuantity);
    }

    [Fact]
    public async Task PostReceiptAsync_WithUnknownIngredient_ReturnsNotFoundWithoutMutation()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);
        repository.AddIngredient(ingredient);
        var receipt = await CreateDraftReceiptWithLinesAsync(ingredient);
        await handler.AddReceiptLineAsync(new AddReceiptLineCommand(receipt.Id, EntityId.New(), 3m, 28m));

        var result = await handler.PostReceiptAsync(new PostReceiptCommand(receipt.Id, ActorId));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("inventory.ingredient_not_found", result.Error.Code);
        Assert.Equal(PurchaseReceiptStatus.Draft, receipt.Status);
        Assert.Equal(10m, ingredient.AvailableQuantity);
    }

    [Fact]
    public async Task PostReceiptAsync_WithUnknownReceipt_ReturnsNotFound()
    {
        var result = await handler.PostReceiptAsync(new PostReceiptCommand(EntityId.New(), ActorId));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("inventory.receipt_not_found", result.Error.Code);
    }

    [Fact]
    public async Task PostReceiptAsync_RunsInsideTransaction()
    {
        var ingredient = CreateIngredient();
        repository.AddIngredient(ingredient);
        var receipt = await CreateDraftReceiptWithLinesAsync(ingredient);

        await handler.PostReceiptAsync(new PostReceiptCommand(receipt.Id, ActorId));

        Assert.Equal(1, transactionManager.ExecutionCount);
        Assert.Equal(1, transactionManager.CommittedCount);
    }

    [Fact]
    public async Task RecordStockAdjustmentAsync_RecordsSignedImmutableMovement()
    {
        var ingredient = CreateIngredient(10m);
        repository.AddIngredient(ingredient);

        var result = await handler.RecordStockAdjustmentAsync(
            new RecordStockAdjustmentCommand(ingredient.Id, -3m, "Spoiled beans", ActorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(7m, ingredient.AvailableQuantity);
        var movement = Assert.Single(repository.StockMovements);
        Assert.Equal(StockMovementType.Adjustment, movement.Type);
        Assert.Equal(-3m, movement.QuantityDelta);
        Assert.Equal("Spoiled beans", movement.Reason);
        Assert.Equal(ActorId, movement.ActorId);
    }

    [Fact]
    public async Task RecordOrderConsumptionAndRestoration_CreateLinkedMovementsExactlyOnce()
    {
        var ingredient = CreateIngredient(10m);
        repository.AddIngredient(ingredient);
        var orderId = EntityId.New();

        var consumed = await handler.RecordOrderConsumptionAsync(
            new RecordOrderConsumptionCommand(orderId, [new StockMovementLine(ingredient.Id, 4m)], ActorId));
        var restored = await handler.RestoreOrderInventoryAsync(
            new RestoreOrderInventoryCommand(orderId, "Order cancelled", ActorId));
        var repeatedRestore = await handler.RestoreOrderInventoryAsync(
            new RestoreOrderInventoryCommand(orderId, "Order cancelled", ActorId));

        Assert.True(consumed.IsSuccess);
        Assert.True(restored.IsSuccess);
        Assert.True(repeatedRestore.IsSuccess);
        Assert.Equal(10m, ingredient.AvailableQuantity);
        Assert.Collection(repository.StockMovements,
            consumption =>
            {
                Assert.Equal(StockMovementType.OrderConsumption, consumption.Type);
                Assert.Equal(-4m, consumption.QuantityDelta);
                Assert.Equal(orderId, consumption.OrderId);
            },
            restoration =>
            {
                Assert.Equal(StockMovementType.Restoration, restoration.Type);
                Assert.Equal(4m, restoration.QuantityDelta);
                Assert.Equal(orderId, restoration.OrderId);
            });
    }
}
