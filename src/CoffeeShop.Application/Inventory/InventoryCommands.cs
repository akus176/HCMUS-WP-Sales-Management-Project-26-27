namespace CoffeeShop.Application.Inventory;

using CoffeeShop.Domain.Common;

public sealed record CreateSupplierCommand(string Name, string Phone, string? Email = null, string? Address = null);

public sealed record UpdateSupplierCommand(EntityId SupplierId, string Name, string Phone, string? Email, string? Address);

public sealed record DeactivateSupplierCommand(EntityId SupplierId);

public sealed record ActivateSupplierCommand(EntityId SupplierId);

public sealed record CreateIngredientCommand(string Name, string BaseUnit, decimal AvailableQuantity = 0, decimal ReorderThreshold = 0);

public sealed record SetReorderThresholdCommand(EntityId IngredientId, decimal ReorderThreshold);

public sealed record DeactivateIngredientCommand(EntityId IngredientId);

public sealed record ActivateIngredientCommand(EntityId IngredientId);

public sealed record CreateDraftReceiptCommand(EntityId SupplierId, string ReceiptNumber);

public sealed record ReceiveDeliveryLine(EntityId IngredientId, decimal Quantity, decimal UnitCost);

public sealed record ReceiveDeliveryCommand(
    EntityId SupplierId, string ReceiptNumber, IReadOnlyList<ReceiveDeliveryLine> Lines, EntityId ActorId);

public sealed record AddReceiptLineCommand(EntityId ReceiptId, EntityId IngredientId, decimal Quantity, decimal UnitCost);

public sealed record RemoveReceiptLineCommand(EntityId ReceiptId, EntityId LineId);

public sealed record CancelReceiptCommand(EntityId ReceiptId);

public sealed record PostReceiptCommand(EntityId ReceiptId, EntityId ActorId);

public sealed record RecordStockAdjustmentCommand(
    EntityId IngredientId,
    decimal QuantityDelta,
    string Reason,
    EntityId ActorId,
    EntityId? AdjustmentId = null);

public sealed record StockMovementLine(EntityId IngredientId, decimal Quantity);

public sealed record RecordOrderConsumptionCommand(
    EntityId OrderId,
    IReadOnlyCollection<StockMovementLine> Lines,
    EntityId ActorId);

public sealed record RestoreOrderInventoryCommand(EntityId OrderId, string Reason, EntityId ActorId);
