using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.Services;

public interface IReportsSource
{
    Task<Result<RevenueReport>> RevenueAsync(DateOnly first, DateOnly last, TimeZoneInfo zone);
    Task<Result<BestSellerReport>> BestSellersAsync(DateOnly first, DateOnly last, TimeZoneInfo zone);
    Task<Result<OrderChannelReport>> ChannelsAsync(DateOnly first, DateOnly last, TimeZoneInfo zone);
    Task<Result<IReadOnlyList<LowStockIngredient>>> LowStockAsync();
}

/// <summary>Explicit midterm and UI-verification report path.</summary>
public sealed class DirectReportsSource(IServiceScopeFactory scopes) : IReportsSource
{
    public Task<Result<RevenueReport>> RevenueAsync(DateOnly first, DateOnly last, TimeZoneInfo zone) =>
        QueryAsync(provider => provider.GetRequiredService<RevenueReportQueryHandler>().GetRevenueAsync(new(first, last, zone)));
    public Task<Result<BestSellerReport>> BestSellersAsync(DateOnly first, DateOnly last, TimeZoneInfo zone) =>
        QueryAsync(provider => provider.GetRequiredService<BestSellerReportQueryHandler>().GetBestSellersAsync(new(first, last, zone)));
    public Task<Result<OrderChannelReport>> ChannelsAsync(DateOnly first, DateOnly last, TimeZoneInfo zone) =>
        QueryAsync(provider => provider.GetRequiredService<OrderChannelReportQueryHandler>().GetChannelsAsync(new(first, last, zone)));
    public Task<Result<IReadOnlyList<LowStockIngredient>>> LowStockAsync() =>
        QueryAsync<IReadOnlyList<LowStockIngredient>>(async provider => Result<IReadOnlyList<LowStockIngredient>>.Success(
            LowStockReport.Build(await provider.GetRequiredService<IInventoryRepository>().ListIngredientsAsync())));

    private async Task<Result<T>> QueryAsync<T>(Func<IServiceProvider, Task<Result<T>>> query) where T : notnull
    {
        using var scope = scopes.CreateScope();
        return await query(scope.ServiceProvider);
    }
}

/// <summary>Final-mode reports always use the authenticated API.</summary>
public sealed class ApiReportsSource(AuthenticatedApiClient client) : IReportsSource
{
    public async Task<Result<RevenueReport>> RevenueAsync(DateOnly first, DateOnly last, TimeZoneInfo zone)
    {
        var data = await client.GetAsync<RevenueReportResponse>($"reports/revenue?{Period(first, last, zone)}");
        return Result<RevenueReport>.Success(new(data.FirstDate, data.LastDateInclusive, data.TimeZoneId,
            data.StartUtc, data.EndUtcExclusive, data.Revenue, data.OrderCount,
            data.Daily.Select(row => new DailyRevenueBreakdown(row.Date, row.Revenue, row.OrderCount)).ToArray(),
            data.ByPaymentMethod.Select(row => new PaymentMethodRevenueBreakdown(row.PaymentMethod, row.Revenue, row.OrderCount)).ToArray()));
    }

    public async Task<Result<BestSellerReport>> BestSellersAsync(DateOnly first, DateOnly last, TimeZoneInfo zone)
    {
        var data = await client.GetAsync<BestSellerReportResponse>($"reports/best-sellers?{Period(first, last, zone)}");
        if (!Enum.TryParse<BestSellerSort>(data.SortBy, true, out var sort) || !Enum.IsDefined(sort))
            throw new InvalidOperationException("The server returned an unsupported report ranking.");
        return Result<BestSellerReport>.Success(new(data.FirstDate, data.LastDateInclusive, data.TimeZoneId,
            data.StartUtc, data.EndUtcExclusive, sort, data.Items.Select(ToRow).ToArray(), data.Variants.Select(ToRow).ToArray()));
    }

    public async Task<Result<OrderChannelReport>> ChannelsAsync(DateOnly first, DateOnly last, TimeZoneInfo zone)
    {
        var data = await client.GetAsync<ChannelReportResponse>($"reports/channels?{Period(first, last, zone)}");
        return Result<OrderChannelReport>.Success(new(data.FirstDate, data.LastDateInclusive, data.TimeZoneId,
            data.StartUtc, data.EndUtcExclusive, data.Revenue, data.OrderCount,
            data.ByChannel.Select(row => new OrderChannelRevenueBreakdown(row.Channel, row.Revenue, row.OrderCount)).ToArray()));
    }

    public async Task<Result<IReadOnlyList<LowStockIngredient>>> LowStockAsync()
    {
        var data = await client.GetAsync<LowStockReportRowResponse[]>("reports/low-stock");
        return Result<IReadOnlyList<LowStockIngredient>>.Success(data.Select(row => new LowStockIngredient(
            new EntityId(row.Id), row.Name, row.BaseUnit, row.AvailableQuantity, row.ReorderThreshold)).ToArray());
    }

    private static string Period(DateOnly first, DateOnly last, TimeZoneInfo zone) =>
        $"from={first:yyyy-MM-dd}&to={last:yyyy-MM-dd}&timeZone={Uri.EscapeDataString(zone.Id)}";

    private static BestSellerRow ToRow(BestSellerRowResponse row) => new(row.Rank,
        row.MenuItemId is { } item ? new EntityId(item) : null,
        row.SizeVariantId is { } size ? new EntityId(size) : null,
        row.ItemName, row.SizeName, row.Quantity, row.Revenue);
}
