namespace CoffeeShop.Application.Customers;

using CoffeeShop.Domain.Common;

public sealed record CustomerSummary(
    EntityId Id,
    string FullName,
    string Phone,
    string? Email,
    bool IsActive,
    int LoyaltyPointsBalance);

public sealed record CustomerAddressSummary(
    EntityId Id,
    string AddressLine,
    string? Label,
    bool IsDefault,
    bool IsActive);

public sealed record CustomerOrderSummary(
    EntityId Id,
    string OrderNumber,
    string Status,
    decimal Total,
    DateTimeOffset CreatedAt);

public sealed record CustomerDetail(
    EntityId Id,
    string FullName,
    string Phone,
    string? Email,
    bool IsActive,
    int LoyaltyPointsBalance,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CustomerAddressSummary> Addresses,
    IReadOnlyList<CustomerOrderSummary> RecentOrders);

public sealed record LoyaltyHistoryCriteria(int Offset = 0, int Limit = 50);

public sealed record LoyaltyTransactionSummary(
    EntityId Id,
    string Type,
    int Points,
    int BalanceAfter,
    EntityId? OrderId,
    EntityId? ReversesTransactionId,
    DateTimeOffset CreatedAt,
    EntityId? CreatedBy);

public sealed record LoyaltyHistoryPage(
    IReadOnlyList<LoyaltyTransactionSummary> Items,
    int Offset,
    int Limit,
    int TotalCount);

public sealed record LoyaltySettingsDetail(
    EntityId Id,
    decimal EarnPointsPerCurrencyUnit,
    decimal RedeemValuePerPoint,
    decimal MaxRedeemablePercentage,
    DateTimeOffset UpdatedAt,
    EntityId? UpdatedBy);
