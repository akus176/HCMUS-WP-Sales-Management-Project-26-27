namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Common;

public sealed record ConfirmOrderCommand(EntityId OrderId, EntityId ActorId);

public sealed record OrderConfirmationResult(EntityId OrderId, string Status, UtcTimestamp ConfirmedAt);
