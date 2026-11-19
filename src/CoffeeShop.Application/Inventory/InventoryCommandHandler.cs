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

    public async Task<Result<PurchaseReceipt>> ReceiveDeliveryAsync(
        ReceiveDeliveryCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var number = command.ReceiptNumber.Trim();
        if (number.Length is 0 or > 50 || command.Lines.Count == 0)
        {
            return Result<PurchaseReceipt>.Failure(Error.Validation("inventory.invalid_delivery",
                "Enter a receipt number of up to 50 characters and at least one ingredient."));
        }

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var existing = await repository.LoadReceiptForReceivingAsync(number, cancellationToken);
            if (existing is not null)
            {
                var savedLines = existing.Lines.Select(line => new ReceiveDeliveryLine(line.IngredientId, line.Quantity, line.UnitCost.Amount));
                if (existing.Status == PurchaseReceiptStatus.Posted && existing.SupplierId == command.SupplierId
                    && SortDeliveryLines(savedLines).SequenceEqual(SortDeliveryLines(command.Lines)))
                {
                    return Result<PurchaseReceipt>.Success(existing);
                }

                return Result<PurchaseReceipt>.Failure(Error.Conflict("inventory.receipt_number_in_use",
                    "This receipt number is already in use. Check Receipts or enter a different number."));
            }

            var supplier = await repository.FindSupplierAsync(command.SupplierId, cancellationToken);
            if (supplier is not { IsActive: true })
            {
                return Result<PurchaseReceipt>.Failure(Error.Validation("inventory.supplier_unavailable", "Choose an active supplier."));
            }

            var receivedAt = clock.UtcNow;
            var receipt = new PurchaseReceipt(EntityId.New(), supplier.Id, number, receivedAt, receivedAt);
            foreach (var line in command.Lines)
            {
                if (line.UnitCost < 0 || line.UnitCost > Money.MaximumAmount || decimal.Round(line.UnitCost, 2) != line.UnitCost)
                {
                    return Result<PurchaseReceipt>.Failure(Error.Validation("inventory.invalid_unit_cost",
                        "Enter a valid non-negative unit cost with up to two decimal places."));
                }
                var added = PurchaseReceiptLifecycle.AddLine(receipt, EntityId.New(), line.IngredientId,
                    line.Quantity, new Money(line.UnitCost), receivedAt);
                if (added.IsFailure) return Result<PurchaseReceipt>.Failure(added.Error!);
            }

            var posted = await ApplyReceiptAsync(receipt, command.ActorId, receivedAt, cancellationToken);
            if (posted.IsFailure) return Result<PurchaseReceipt>.Failure(posted.Error!);
            repository.AddReceipt(receipt);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<PurchaseReceipt>.Success(receipt);
        }, cancellationToken);
    }

    private static IEnumerable<ReceiveDeliveryLine> SortDeliveryLines(IEnumerable<ReceiveDeliveryLine> lines) =>
        lines.OrderBy(line => line.IngredientId.Value).ThenBy(line => line.Quantity).ThenBy(line => line.UnitCost);

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

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var receipt = await repository.LoadReceiptForPostingAsync(command.ReceiptId, cancellationToken);
            if (receipt is null) return Result.Failure(ReceiptNotFound(command.ReceiptId));

            var posted = await ApplyReceiptAsync(receipt, command.ActorId, clock.UtcNow, cancellationToken);
            if (posted.IsFailure) return posted;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }, cancellationToken);
    }

    private async Task<Result> ApplyReceiptAsync(PurchaseReceipt receipt, EntityId actorId, UtcTimestamp receivedAt,
        CancellationToken cancellationToken)
    {
        var ids = receipt.Lines.Select(line => line.IngredientId).Distinct().ToArray();
        var ingredients = await repository.LoadIngredientsForStockUpdateAsync(ids, cancellationToken);
        var byId = ingredients.ToDictionary(ingredient => ingredient.Id);
        var posted = PurchaseReceiptLifecycle.PostReceipt(receipt, id => byId.GetValueOrDefault(id), receivedAt);
        if (posted.IsFailure) return Result.Failure(posted.Error!);

        foreach (var line in receipt.Lines)
        {
            repository.AddStockMovement(new StockMovement(EntityId.New(), line.IngredientId, StockMovementType.Receipt,
                line.Quantity, actorId, $"Purchase receipt {receipt.ReceiptNumber}", receivedAt, purchaseReceiptId: receipt.Id));
        }
        return Result.Success();
    }

    public async Task<Result> RecordStockAdjustmentAsync(
        RecordStockAdjustmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result.Failure(Error.Validation("inventory.adjustment_reason_required", "A stock adjustment requires a reason."));
        }

        if (command.ActorId is null)
        {
            return Result.Failure(Error.Validation("inventory.adjustment_actor_required", "A stock adjustment requires an actor identity."));
        }

        try
        {
            return await transactionManager.ExecuteInTransactionAsync(async () =>
            {
                if (command.AdjustmentId is not null)
                {
                    await repository.LockAdjustmentRequestAsync(command.AdjustmentId, cancellationToken);
                    var existing = await repository.FindStockMovementAsync(command.AdjustmentId, cancellationToken);
                    if (existing is not null)
                    {
                        return existing.Type == StockMovementType.Adjustment && existing.IngredientId == command.IngredientId &&
                            existing.QuantityDelta == command.QuantityDelta && existing.Reason == command.Reason.Trim() &&
                            existing.ActorId == command.ActorId
                            ? Result.Success()
                            : Result.Failure(Error.Conflict("inventory.adjustment_retry_mismatch",
                                "The adjustment identifier is already associated with different data."));
                    }
                }
                var ingredient = command.AdjustmentId is null
                    ? await repository.FindIngredientAsync(command.IngredientId, cancellationToken)
                    : await repository.LoadIngredientForAdjustmentAsync(command.IngredientId, cancellationToken);
                if (ingredient is null) return Result.Failure(IngredientNotFound(command.IngredientId));
                ingredient.Adjust(command.QuantityDelta, clock.UtcNow);
                repository.AddStockMovement(new StockMovement(
                    command.AdjustmentId ?? EntityId.New(), ingredient.Id, StockMovementType.Adjustment, command.QuantityDelta,
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

        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length > 255)
        {
            return Result.Failure(Error.Validation("inventory.restoration_reason_required", "Inventory restoration requires a reason of at most 255 characters."));
        }

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            await repository.LockOrderForRestorationAsync(command.OrderId, cancellationToken);
            if (await repository.HasOrderRestorationAsync(command.OrderId, cancellationToken))
            {
                return Result.Success();
            }

            var movements = await repository.ListMovementsForOrderAsync(command.OrderId, cancellationToken);
            // The immutable consumption ledger is authoritative, even if recipes changed.
            // Account for restoration movements made before the durable guard was introduced.
            var quantities = movements.Where(movement => movement.Type is StockMovementType.OrderConsumption or StockMovementType.Restoration)
                .GroupBy(movement => movement.IngredientId)
                .Select(group => (IngredientId: group.Key, Quantity: -group.Sum(movement => movement.QuantityDelta)))
                .ToArray();
            if (quantities.Any(line => line.Quantity < 0))
            {
                return Result.Failure(Error.Conflict("inventory.invalid_restoration_history", "The order's restoration history exceeds its recorded consumption."));
            }

            var pending = quantities.Where(line => line.Quantity > 0).ToArray();
            var ingredients = (await repository.LoadIngredientsForStockUpdateAsync(
                pending.Select(line => line.IngredientId).ToArray(), cancellationToken)).ToDictionary(ingredient => ingredient.Id);
            foreach (var line in pending)
            {
                if (!ingredients.TryGetValue(line.IngredientId, out var ingredient))
                {
                    return Result.Failure(IngredientNotFound(line.IngredientId));
                }

                if (line.Quantity + ingredient.AvailableQuantity > MeasuredQuantity.MaximumValue)
                {
                    return Result.Failure(Error.Conflict("inventory.restoration_balance_conflict", "Restoration would exceed the supported ingredient balance range."));
                }
            }

            var restoredAt = clock.UtcNow;
            foreach (var line in pending)
            {
                var ingredient = ingredients[line.IngredientId];
                ingredient.Receive(line.Quantity, restoredAt);
                repository.AddStockMovement(new StockMovement(
                    EntityId.New(), ingredient.Id, StockMovementType.Restoration, line.Quantity,
                    command.ActorId, command.Reason, restoredAt, orderId: command.OrderId));
            }

            repository.AddOrderRestoration(new OrderInventoryRestoration(command.OrderId, restoredAt, command.ActorId));
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
