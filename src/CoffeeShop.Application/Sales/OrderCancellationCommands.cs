namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Common;

public sealed record CancelOrderCommand(EntityId OrderId, string Reason);

public sealed record OrderCancellationResult(EntityId OrderId, string Status, UtcTimestamp CancelledAt);
