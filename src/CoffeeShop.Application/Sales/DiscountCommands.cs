namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Common;

public sealed record RequestDiscountCommand(
    EntityId DiscountId, EntityId OrderId, string Type, decimal Value, string Reason);

public sealed record ApproveDiscountCommand(EntityId DiscountId);

public sealed record RejectDiscountCommand(EntityId DiscountId);

public sealed record DiscountOperationResult(
    EntityId DiscountId, string Status, decimal? ComputedAmount, decimal DiscountTotal, decimal Total);
