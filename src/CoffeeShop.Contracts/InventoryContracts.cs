namespace CoffeeShop.Contracts;

public sealed record CreateIngredientRequest(string? Name, string? BaseUnit, decimal AvailableQuantity,
    decimal ReorderThreshold);

public sealed record StockMovementResponse(Guid Id, Guid IngredientId, string Type, decimal QuantityDelta,
    Guid ActorId, string Reason, DateTimeOffset OccurredAt, Guid? PurchaseReceiptId, Guid? OrderId);

public sealed record SetReorderThresholdRequest(decimal ReorderThreshold);
public sealed record SupplierRequest(string? Name, string? Phone, string? Email, string? Address);
public sealed record SupplierResponse(Guid Id, string Name, string Phone, string? Email, string? Address, bool IsActive);
public sealed record CreateReceiptRequest(Guid SupplierId, string? ReceiptNumber);
public sealed record ReceiptLineRequest(Guid IngredientId, decimal Quantity, decimal UnitCost);
public sealed record ReceiptLineResponse(Guid Id, Guid IngredientId, decimal Quantity, decimal UnitCost);
public sealed record ReceiptResponse(Guid Id, Guid SupplierId, string ReceiptNumber, string Status,
    DateTimeOffset? PostedAt, IReadOnlyList<ReceiptLineResponse> Lines);
