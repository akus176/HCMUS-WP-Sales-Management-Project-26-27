namespace CoffeeShop.Contracts;

public sealed record RevenueDayResponse(DateOnly Date, decimal Revenue, int OrderCount);
public sealed record PaymentRevenueResponse(string PaymentMethod, decimal Revenue, int OrderCount);
public sealed record RevenueReportResponse(DateOnly FirstDate, DateOnly LastDateInclusive, string TimeZoneId,
    DateTimeOffset StartUtc, DateTimeOffset EndUtcExclusive, decimal Revenue, int OrderCount,
    IReadOnlyList<RevenueDayResponse> Daily, IReadOnlyList<PaymentRevenueResponse> ByPaymentMethod);

public sealed record BestSellerRowResponse(int Rank, Guid? MenuItemId, Guid? SizeVariantId,
    string ItemName, string? SizeName, long Quantity, decimal Revenue);
public sealed record BestSellerReportResponse(DateOnly FirstDate, DateOnly LastDateInclusive, string TimeZoneId,
    DateTimeOffset StartUtc, DateTimeOffset EndUtcExclusive, string SortBy,
    IReadOnlyList<BestSellerRowResponse> Items, IReadOnlyList<BestSellerRowResponse> Variants);

public sealed record ChannelRevenueResponse(string Channel, decimal Revenue, int OrderCount);
public sealed record ChannelReportResponse(DateOnly FirstDate, DateOnly LastDateInclusive, string TimeZoneId,
    DateTimeOffset StartUtc, DateTimeOffset EndUtcExclusive, decimal Revenue, int OrderCount,
    IReadOnlyList<ChannelRevenueResponse> ByChannel);

public sealed record LowStockReportRowResponse(Guid Id, string Name, string BaseUnit,
    decimal AvailableQuantity, decimal ReorderThreshold);
