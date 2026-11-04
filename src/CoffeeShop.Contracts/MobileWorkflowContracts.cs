namespace CoffeeShop.Contracts;

// These transport shapes reserve the P0 wire contract. They are not published in
// OpenAPI until the corresponding HTTP routes are implemented.
public sealed record PreparationTicketLine(string ItemName, string SizeName, int Quantity,
    IReadOnlyList<string> Addons);

public sealed record PreparationTicketResponse(Guid OrderId, string OrderNumber, string Status,
    string? TableNumber, IReadOnlyList<PreparationTicketLine> Lines);

public sealed record PendingDiscountResponse(Guid DiscountId, Guid OrderId, string OrderNumber,
    string Type, decimal Value, string Reason, decimal? ComputedAmount, decimal OrderTotal);

public sealed record DiscountDecisionResponse(Guid DiscountId, string Status, decimal? ComputedAmount,
    decimal DiscountTotal, decimal OrderTotal);

public sealed record IngredientBalanceResponse(Guid IngredientId, string Name, string BaseUnit,
    decimal AvailableQuantity, decimal ReorderThreshold);

// Actor identity is always read from the authenticated session by the API.
public sealed record StockAdjustmentRequest(Guid RequestId, decimal QuantityDelta, string? Reason);

public sealed record StockAdjustmentResponse(Guid IngredientId, decimal AvailableQuantity);

// Assignment lists carry no recipient contact information. Detail is fetched separately.
public sealed record CourierAssignmentResponse(Guid OrderId, string OrderNumber, string DeliveryStatus);

public sealed record CourierAssignmentDetailResponse(Guid OrderId, string OrderNumber, string DeliveryStatus,
    string RecipientName, string ContactNumber, string AddressLine);

public sealed record FailDeliveryRequest(string? Reason);

public sealed record AssignDeliveryRequest(Guid StaffId);
