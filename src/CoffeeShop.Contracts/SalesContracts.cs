namespace CoffeeShop.Contracts;

public sealed record OrderCustomerRequest(Guid? CustomerId = null, Guid? CustomerAddressId = null,
    string? RecipientName = null, string? ContactNumber = null, string? AddressLine = null);
public sealed record OrderLineRequest(Guid MenuItemId, Guid SizeVariantId, int Quantity, IReadOnlyList<Guid>? AddonIds);
public sealed record CreateOrderRequest(Guid OrderId, string? Channel, Guid? TableId,
    OrderCustomerRequest? Customer, IReadOnlyList<OrderLineRequest>? Lines);
public sealed record ConfirmOrderRequest(decimal ReviewedTotal);
public sealed record OrderLineResponse(string ItemName, string SizeName, int Quantity, decimal UnitPrice,
    decimal Total, IReadOnlyList<string> Addons);
public sealed record OrderDiscountResponse(Guid Id, string Type, decimal Value, string Status, decimal? Amount);
public sealed record OrderResponse(Guid Id, string OrderNumber, string Channel, string Status, Guid? CustomerId,
    Guid? TableId, string? TableNumber, decimal Subtotal, decimal DiscountTotal, decimal Total,
    IReadOnlyList<OrderLineResponse> Lines, IReadOnlyList<OrderDiscountResponse> Discounts);
public sealed record OperationalOrderResponse(Guid Id, string OrderNumber, string Channel, string Status,
    decimal Total, decimal PaidAmount, string? TableNumber, string? DeliveryStatus, Guid? AssignedStaffId,
    IReadOnlyList<Guid> PendingDiscountIds);
public sealed record RecordPaymentRequest(Guid PaymentId, string? Method, decimal Amount,
    string? Status = null, string? Reference = null);
public sealed record DiscountRequest(Guid DiscountId, string? Type, decimal Value, string? Reason);
public sealed record CancelOrderRequest(string? Reason);
