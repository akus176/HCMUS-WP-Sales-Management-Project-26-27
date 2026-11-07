namespace CoffeeShop.Api;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;
using CoffeeShop.Contracts;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this RouteGroupBuilder api)
    {
        var reports = api.MapGroup("/reports");
        reports.MapGet("/revenue", async (DateOnly from, DateOnly to, string? timeZone,
            RevenueReportQueryHandler handler, CancellationToken ct) =>
        {
            var zone = ResolveTimeZone(timeZone);
            if (zone is null) return InvalidTimeZone();
            var result = await handler.GetRevenueAsync(new(from, to, zone), ct);
            if (result.IsFailure) return ApiErrorResults.From(result.Error!);
            var value = result.Value;
            return Results.Ok(new RevenueReportResponse(value.FirstDate, value.LastDateInclusive, value.TimeZoneId,
                value.StartUtc, value.EndUtcExclusive, value.Revenue, value.OrderCount,
                value.Daily.Select(row => new RevenueDayResponse(row.Date, row.Revenue, row.OrderCount)).ToArray(),
                value.ByPaymentMethod.Select(row => new PaymentRevenueResponse(row.PaymentMethod, row.Revenue, row.OrderCount)).ToArray()));
        }).RequireAuthorization("reports.revenue").Produces<RevenueReportResponse>().ProducesProblem(400).ProducesProblem(403);

        reports.MapGet("/best-sellers", async (DateOnly from, DateOnly to, string? timeZone, string? sortBy, int? limit,
            BestSellerReportQueryHandler handler, CancellationToken ct) =>
        {
            var zone = ResolveTimeZone(timeZone);
            if (zone is null) return InvalidTimeZone();
            var sort = sortBy?.ToLowerInvariant() switch
            {
                null or "quantity" => BestSellerSort.Quantity,
                "revenue" => BestSellerSort.Revenue,
                _ => (BestSellerSort)(-1),
            };
            var result = await handler.GetBestSellersAsync(new(from, to, zone, sort, limit ?? 10), ct);
            if (result.IsFailure) return ApiErrorResults.From(result.Error!);
            var value = result.Value;
            return Results.Ok(new BestSellerReportResponse(value.FirstDate, value.LastDateInclusive, value.TimeZoneId,
                value.StartUtc, value.EndUtcExclusive, value.SortBy.ToString().ToLowerInvariant(),
                value.Items.Select(ToRow).ToArray(), value.Variants.Select(ToRow).ToArray()));
        }).RequireAuthorization("reports.bestsellers").Produces<BestSellerReportResponse>().ProducesProblem(400).ProducesProblem(403);

        reports.MapGet("/channels", async (DateOnly from, DateOnly to, string? timeZone,
            OrderChannelReportQueryHandler handler, CancellationToken ct) =>
        {
            var zone = ResolveTimeZone(timeZone);
            if (zone is null) return InvalidTimeZone();
            var result = await handler.GetChannelsAsync(new(from, to, zone), ct);
            if (result.IsFailure) return ApiErrorResults.From(result.Error!);
            var value = result.Value;
            return Results.Ok(new ChannelReportResponse(value.FirstDate, value.LastDateInclusive, value.TimeZoneId,
                value.StartUtc, value.EndUtcExclusive, value.Revenue, value.OrderCount,
                value.ByChannel.Select(row => new ChannelRevenueResponse(row.Channel, row.Revenue, row.OrderCount)).ToArray()));
        }).RequireAuthorization("reports.channels").Produces<ChannelReportResponse>().ProducesProblem(400).ProducesProblem(403);

        reports.MapGet("/low-stock", async (IInventoryRepository inventory, CancellationToken ct) =>
            Results.Ok(LowStockReport.Build(await inventory.ListIngredientsAsync(ct))
                .Select(row => new LowStockReportRowResponse(row.Id.Value, row.Name, row.BaseUnit,
                    row.AvailableQuantity, row.ReorderThreshold)).ToArray()))
            .RequireAuthorization("reports.lowstock").Produces<LowStockReportRowResponse[]>().ProducesProblem(403);
    }

    private static BestSellerRowResponse ToRow(BestSellerRow row) => new(row.Rank, row.MenuItemId?.Value,
        row.SizeVariantId?.Value, row.ItemName, row.SizeName, row.Quantity, row.Revenue);

    private static TimeZoneInfo? ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
    }

    private static IResult InvalidTimeZone() => ApiErrorResults.From(
        Error.Validation("reports.invalid_time_zone", "Specify a valid business time zone."));
}
