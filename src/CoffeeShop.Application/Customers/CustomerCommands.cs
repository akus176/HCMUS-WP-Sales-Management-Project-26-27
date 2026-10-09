namespace CoffeeShop.Application.Customers;

using CoffeeShop.Domain.Common;

public sealed record CreateCustomerCommand(string FullName, string Phone, string? Email = null);

public sealed record UpdateCustomerCommand(EntityId CustomerId, string FullName, string Phone, string? Email = null);

public sealed record DeactivateCustomerCommand(EntityId CustomerId);

public sealed record AddCustomerAddressCommand(
    EntityId CustomerId,
    string AddressLine,
    string? Label = null,
    bool IsDefault = false);

public sealed record UpdateCustomerAddressCommand(
    EntityId CustomerId,
    EntityId AddressId,
    string AddressLine,
    string? Label,
    bool IsDefault);

public sealed record SetDefaultCustomerAddressCommand(EntityId CustomerId, EntityId AddressId);

public sealed record DeactivateCustomerAddressCommand(EntityId CustomerId, EntityId AddressId);

public sealed record EarnOrderLoyaltyCommand(EntityId OrderId);

public sealed record RedeemOrderLoyaltyCommand(EntityId OrderId, int Points);

public sealed record ReverseCancelledOrderLoyaltyCommand(EntityId OrderId);

public sealed record UpdateLoyaltySettingsCommand(
    decimal EarnPointsPerCurrencyUnit,
    decimal RedeemValuePerPoint,
    decimal MaxRedeemablePercentage);

public sealed record LoyaltyOperationResult(
    EntityId? TransactionId,
    int Points,
    decimal DiscountAmount,
    bool AlreadyApplied);

public sealed record LoyaltyReversalResult(
    IReadOnlyList<EntityId> TransactionIds,
    bool AlreadyApplied);
