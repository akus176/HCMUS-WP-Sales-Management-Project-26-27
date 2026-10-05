namespace CoffeeShop.Application.Inventory;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Domain.Common;

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

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }, cancellationToken);
    }

    private static Error SupplierNotFound(EntityId id) =>
        Error.NotFound("inventory.supplier_not_found", $"Supplier '{id}' was not found.");

    private static Error ReceiptNotFound(EntityId id) =>
        Error.NotFound("inventory.receipt_not_found", $"Purchase receipt '{id}' was not found.");
}
