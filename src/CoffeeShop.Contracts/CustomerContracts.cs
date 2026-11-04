namespace CoffeeShop.Contracts;

public sealed record CustomerUpsertRequest(string? FullName, string? Phone, string? Email = null);
public sealed record CustomerResponse(Guid Id, string FullName, string Phone, string? Email,
    bool IsActive, int LoyaltyPointsBalance);
public sealed record CustomerAddressRequest(string? AddressLine, string? Label = null, bool IsDefault = false);
public sealed record CustomerAddressResponse(Guid Id, string AddressLine, string? Label,
    bool IsDefault, bool IsActive);
public sealed record CustomerOrderSummaryResponse(Guid Id, string OrderNumber, string Status,
    decimal Total, DateTimeOffset CreatedAt);
public sealed record CustomerDetailResponse(Guid Id, string FullName, string Phone, string? Email,
    bool IsActive, int LoyaltyPointsBalance, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<CustomerAddressResponse> Addresses, IReadOnlyList<CustomerOrderSummaryResponse> RecentOrders);
public sealed record LoyaltyTransactionResponse(Guid Id, string Type, int Points, int BalanceAfter,
    Guid? OrderId, Guid? ReversesTransactionId, DateTimeOffset CreatedAt, Guid? CreatedBy);
public sealed record LoyaltyHistoryResponse(IReadOnlyList<LoyaltyTransactionResponse> Items,
    int Offset, int Limit, int TotalCount);
public sealed record LoyaltySettingsRequest(decimal EarnPointsPerCurrencyUnit, decimal RedeemValuePerPoint,
    decimal MaxRedeemablePercentage);
public sealed record LoyaltySettingsResponse(Guid Id, decimal EarnPointsPerCurrencyUnit,
    decimal RedeemValuePerPoint, decimal MaxRedeemablePercentage, DateTimeOffset UpdatedAt, Guid? UpdatedBy);
public sealed record RedeemLoyaltyRequest(int Points);
public sealed record LoyaltyOperationResponse(Guid? TransactionId, int Points,
    decimal DiscountAmount, bool AlreadyApplied);
