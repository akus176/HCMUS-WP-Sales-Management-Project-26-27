namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Common;

public sealed record AssignDeliveryCommand(EntityId OrderId, EntityId StaffId, EntityId ActorId);
public sealed record StartDeliveryCommand(EntityId OrderId, EntityId ActorId);
public sealed record CompleteDeliveryCommand(EntityId OrderId, EntityId ActorId);
public sealed record FailDeliveryCommand(EntityId OrderId, string Reason, EntityId ActorId);
