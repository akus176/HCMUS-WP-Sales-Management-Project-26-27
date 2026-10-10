namespace CoffeeShop.Application.Inventory;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public sealed class InventoryCommandHandler
{
    private readonly IInventoryRepository repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly ITransactionManager transactionManager;
    private readonly IClock clock;

    public InventoryCommandHandler(
        IInventoryRepository repository,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager,
        IClock clock)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        this.transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<EntityId>> CreateSupplierAsync(
        CreateSupplierCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var created = SupplierLifecycle.CreateSupplier(
            EntityId.New(),
            command.Name,
            command.Phone,
            clock.UtcNow,
            command.Email,
            command.Address);
        if (created.IsFailure)
        {
            return Result<EntityId>.Failure(created.Error!);
        }

        repository.AddSupplier(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EntityId>.Success(created.Value.Id);
    }

    public async Task<Result> UpdateSupplierAsync(
        UpdateSupplierCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var supplier = await repository.FindSupplierAsync(command.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierNotFound(command.SupplierId));
        }

        var updated = SupplierLifecycle.UpdateSupplier(
            supplier,
            command.Name,
            command.Phone,
            command.Email,
            command.Address,
            clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateSupplierAsync(
        DeactivateSupplierCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var supplier = await repository.FindSupplierAsync(command.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierNotFound(command.SupplierId));
        }

        var deactivated = SupplierLifecycle.DeactivateSupplier(supplier, clock.UtcNow);
        if (deactivated.IsFailure)
        {
            return deactivated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ActivateSupplierAsync(
        ActivateSupplierCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var supplier = await repository.FindSupplierAsync(command.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierNotFound(command.SupplierId));
        }

        var activated = SupplierLifecycle.ActivateSupplier(supplier, clock.UtcNow);
        if (activated.IsFailure)
        {
            return activated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<EntityId>> CreateDraftReceiptAsync(
        CreateDraftReceiptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var supplier = await repository.FindSupplierAsync(command.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result<EntityId>.Failure(Error.NotFound(
                "inventory.supplier_not_found",
                $"Supplier '{command.SupplierId}' was not found."));
        }

        var created = PurchaseReceiptLifecycle.CreateDraftReceipt(
            EntityId.New(),
            command.SupplierId,
            command.ReceiptNumber,
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result<EntityId>.Failure(created.Error!);
        }

        repository.AddReceipt(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EntityId>.Success(created.Value.Id);
    }

    public async Task<Result<EntityId>> AddReceiptLineAsync(
        AddReceiptLineCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var receipt = await repository.FindReceiptWithLinesAsync(command.ReceiptId, cancellationToken);
        if (receipt is null)
        {
            return Result<EntityId>.Failure(ReceiptNotFound(command.ReceiptId));
        }

        var added = PurchaseReceiptLifecycle.AddLine(
            receipt,
            EntityId.New(),
            command.IngredientId,
            command.Quantity,
            new Money(command.UnitCost),
            clock.UtcNow);
        if (added.IsFailure)
        {
            return Result<EntityId>.Failure(added.Error!);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EntityId>.Success(added.Value.Id);
    }

    public async Task<Result> RemoveReceiptLineAsync(
        RemoveReceiptLineCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var receipt = await repository.FindReceiptWithLinesAsync(command.ReceiptId, cancellationToken);
        if (receipt is null)
        {
            return Result.Failure(ReceiptNotFound(command.ReceiptId));
        }

        var removed = PurchaseReceiptLifecycle.RemoveLine(receipt, command.LineId, clock.UtcNow);
        if (removed.IsFailure)
        {
            return removed;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CancelReceiptAsync(
        CancelReceiptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var receipt = await repository.FindReceiptWithLinesAsync(command.ReceiptId, cancellationToken);
        if (receipt is null)
        {
            return Result.Failure(ReceiptNotFound(command.ReceiptId));
        }

        var cancelled = PurchaseReceiptLifecycle.CancelReceipt(receipt, clock.UtcNow);
        if (cancelled.IsFailure)
        {
            return cancelled;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Posts a receipt and applies every line's quantity to the matching
    /// ingredient. The receipt and all changed ingredients are saved in a
    /// single transaction, so posting either completes fully or not at all.
    /// </summary>
    public async Task<Result> PostReceiptAsync(
        PostReceiptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var receipt = await repository.FindReceiptWithLinesAsync(command.ReceiptId, cancellationToken);
        if (receipt is null)
        {
            return Result.Failure(ReceiptNotFound(command.ReceiptId));
        }

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var posted = PurchaseReceiptLifecycle.PostReceipt(
                receipt,
                id => repository.FindIngredientAsync(id, cancellationToken).GetAwaiter().GetResult(),
                clock.UtcNow);
            if (posted.IsFailure)
            {
                return Result.Failure(posted.Error!);
            }

            foreach (var line in receipt.Lines)
            {
                repository.AddStockMovement(new StockMovement(
                    EntityId.New(),
                    line.IngredientId,
                    StockMovementType.Receipt,
                    line.Quantity,
                    command.ActorId,
                    $"Purchase receipt {receipt.ReceiptNumber}",
                    clock.UtcNow,
                    purchaseReceiptId: receipt.Id));
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }, cancellationToken);
    }

    public async Task<Result> RecordStockAdjustmentAsync(
        RecordStockAdjustmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ingredient = await repository.FindIngredientAsync(command.IngredientId, cancellationToken);
        if (ingredient is null)
        {
            return Result.Failure(IngredientNotFound(command.IngredientId));
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result.Failure(Error.Validation("inventory.adjustment_reason_required", "A stock adjustment requires a reason."));
        }

        try
        {
            return await transactionManager.ExecuteInTransactionAsync(async () =>
            {
                ingredient.Adjust(command.QuantityDelta, clock.UtcNow);
                repository.AddStockMovement(new StockMovement(
                    EntityId.New(), ingredient.Id, StockMovementType.Adjustment, command.QuantityDelta,
                    command.ActorId, command.Reason, clock.UtcNow));
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success();
            }, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure(Error.Validation("inventory.invalid_adjustment", exception.Message));
        }
    }

    public async Task<Result> RecordOrderConsumptionAsync(
        RecordOrderConsumptionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Lines is null || command.Lines.Count == 0)
        {
            return Result.Failure(Error.Validation("inventory.consumption_lines_required", "Order consumption requires at least one ingredient line."));
        }

        var existing = await repository.ListMovementsForOrderAsync(command.OrderId, cancellationToken);
        if (existing.Any(m => m.Type == StockMovementType.OrderConsumption))
        {
            return Result.Failure(Error.Conflict("inventory.order_already_consumed", $"Order '{command.OrderId}' already has inventory consumption records."));
        }

        var requested = command.Lines
            .GroupBy(line => line.IngredientId)
            .Select(group => new StockMovementLine(group.Key, group.Sum(line => line.Quantity)))
            .ToArray();
        var ingredients = new List<(StockMovementLine Line, Ingredient Ingredient)>();

        foreach (var line in requested)
        {
            var ingredient = await repository.FindIngredientAsync(line.IngredientId, cancellationToken);
            if (ingredient is null)
            {
                return Result.Failure(IngredientNotFound(line.IngredientId));
            }

            if (line.Quantity <= 0 || line.Quantity > ingredient.AvailableQuantity)
            {
                return Result.Failure(Error.BusinessRule(
                    "inventory.insufficient_stock",
                    $"Ingredient '{ingredient.Name}' does not have sufficient stock for this order."));
            }

            ingredients.Add((line, ingredient));
        }

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            foreach (var (line, ingredient) in ingredients)
            {
                ingredient.Consume(line.Quantity, clock.UtcNow);
                repository.AddStockMovement(new StockMovement(
                    EntityId.New(), ingredient.Id, StockMovementType.OrderConsumption, -line.Quantity,
                    command.ActorId, $"Order {command.OrderId} consumption", clock.UtcNow, orderId: command.OrderId));
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }, cancellationToken);
    }

    public async Task<Result> RestoreOrderInventoryAsync(
        RestoreOrderInventoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result.Failure(Error.Validation("inventory.restoration_reason_required", "Inventory restoration requires a reason."));
        }

        var movements = await repository.ListMovementsForOrderAsync(command.OrderId, cancellationToken);
        if (movements.Any(m => m.Type == StockMovementType.Restoration))
        {
            return Result.Success();
        }

        var consumptions = movements.Where(m => m.Type == StockMovementType.OrderConsumption).ToArray();
        if (consumptions.Length == 0)
        {
            return Result.Failure(Error.NotFound("inventory.order_consumption_not_found", $"Order '{command.OrderId}' has no inventory consumption to restore."));
        }

        var ingredients = new List<(StockMovement Consumption, Ingredient Ingredient)>();
        foreach (var consumption in consumptions)
        {
            var ingredient = await repository.FindIngredientAsync(consumption.IngredientId, cancellationToken);
            if (ingredient is null)
            {
                return Result.Failure(IngredientNotFound(consumption.IngredientId));
            }

            ingredients.Add((consumption, ingredient));
        }

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            foreach (var (consumption, ingredient) in ingredients)
            {
                var restoredQuantity = -consumption.QuantityDelta;
                ingredient.Receive(restoredQuantity, clock.UtcNow);
                repository.AddStockMovement(new StockMovement(
                    EntityId.New(), ingredient.Id, StockMovementType.Restoration, restoredQuantity,
                    command.ActorId, command.Reason, clock.UtcNow, orderId: command.OrderId));
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }, cancellationToken);
    }

    private static Error SupplierNotFound(EntityId id) =>
        Error.NotFound("inventory.supplier_not_found", $"Supplier '{id}' was not found.");

    private static Error ReceiptNotFound(EntityId id) =>
        Error.NotFound("inventory.receipt_not_found", $"Purchase receipt '{id}' was not found.");

    private static Error IngredientNotFound(EntityId id) =>
        Error.NotFound("inventory.ingredient_not_found", $"Ingredient '{id}' was not found.");

    public async Task<Result<EntityId>> CreateIngredientAsync(
        CreateIngredientCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var created = IngredientLifecycle.CreateIngredient(
            EntityId.New(),
            command.Name,
            command.BaseUnit,
            clock.UtcNow,
            command.AvailableQuantity,
            command.ReorderThreshold);
        if (created.IsFailure)
        {
            return Result<EntityId>.Failure(created.Error!);
        }

        repository.AddIngredient(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EntityId>.Success(created.Value.Id);
    }

    public async Task<Result> SetReorderThresholdAsync(
        SetReorderThresholdCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ingredient = await repository.FindIngredientAsync(command.IngredientId, cancellationToken);
        if (ingredient is null)
        {
            return Result.Failure(IngredientNotFound(command.IngredientId));
        }

        var updated = IngredientLifecycle.SetReorderThreshold(ingredient, command.ReorderThreshold, clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateIngredientAsync(
        DeactivateIngredientCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ingredient = await repository.FindIngredientAsync(command.IngredientId, cancellationToken);
        if (ingredient is null)
        {
            return Result.Failure(IngredientNotFound(command.IngredientId));
        }

        var deactivated = IngredientLifecycle.DeactivateIngredient(ingredient, clock.UtcNow);
        if (deactivated.IsFailure)
        {
            return deactivated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ActivateIngredientAsync(
        ActivateIngredientCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ingredient = await repository.FindIngredientAsync(command.IngredientId, cancellationToken);
        if (ingredient is null)
        {
            return Result.Failure(IngredientNotFound(command.IngredientId));
        }

        var activated = IngredientLifecycle.ActivateIngredient(ingredient, clock.UtcNow);
        if (activated.IsFailure)
        {
            return activated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
