namespace CoffeeShop.Application.Inventory;

using CoffeeShop.Domain.Common;

public sealed record CreateSupplierCommand(string Name, string Phone, string? Email = null, string? Address = null);

public sealed record UpdateSupplierCommand(EntityId SupplierId, string Name, string Phone, string? Email, string? Address);

public sealed record DeactivateSupplierCommand(EntityId SupplierId);

public sealed record ActivateSupplierCommand(EntityId SupplierId);

public sealed record CreateDraftReceiptCommand(EntityId SupplierId, string ReceiptNumber);

public sealed record AddReceiptLineCommand(EntityId ReceiptId, EntityId IngredientId, decimal Quantity, decimal UnitCost);

public sealed record RemoveReceiptLineCommand(EntityId ReceiptId, EntityId LineId);

public sealed record CancelReceiptCommand(EntityId ReceiptId);

public sealed record PostReceiptCommand(EntityId ReceiptId);
