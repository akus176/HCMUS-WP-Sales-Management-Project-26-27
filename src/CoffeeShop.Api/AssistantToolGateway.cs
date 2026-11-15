namespace CoffeeShop.Api;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;
using CoffeeShop.Contracts;

/// <summary>Single allowlist boundary between provider-selected tools and report queries.</summary>
public interface IAssistantToolGateway
{
    Task<Result<AssistantToolResult>> InvokeAsync(string tool, AssistantToolInput input,
        CancellationToken cancellationToken = default);
}

public sealed class AssistantToolGateway(
    ICurrentUser user,
    RevenueReportQueryHandler revenue,
    BestSellerReportQueryHandler bestSellers,
    OrderChannelReportQueryHandler channels,
    IInventoryRepository inventory) : IAssistantToolGateway
{
    public async Task<Result<AssistantToolResult>> InvokeAsync(
        string tool, AssistantToolInput input, CancellationToken cancellationToken = default)
    {
        if (input is null || tool is not (AssistantTools.Revenue or AssistantTools.BestSellers or
            AssistantTools.Channels or AssistantTools.LowStock))
            return Fail("assistant.unsupported_tool", "Only approved read-only reporting tools are available.");

        if (!user.IsAuthenticated || !user.HasPermission("assistant.ask") ||
            !(user.IsInRole("Manager") || user.IsInRole("Admin")) ||
            !user.HasPermission(tool switch
            {
                AssistantTools.Revenue => "reports.revenue",
                AssistantTools.BestSellers => "reports.bestsellers",
                AssistantTools.Channels => "reports.channels",
                _ => "reports.lowstock",
            }))
            return Result<AssistantToolResult>.Failure(Error.Authorization(
                "assistant.forbidden", "You do not have permission to use this report."));

        if (tool == AssistantTools.LowStock)
        {
            if (input.From is not null || input.To is not null || input.TimeZone is not null ||
                input.SortBy is not null || input.Limit is not null)
                return Fail("assistant.invalid_tool_input", "Low-stock reporting accepts no parameters.");
            var rows = LowStockReport.Build(await inventory.ListIngredientsAsync(cancellationToken))
                .Select(row => new LowStockReportRowResponse(row.Id.Value, row.Name, row.BaseUnit,
                    row.AvailableQuantity, row.ReorderThreshold)).ToArray();
            return Result<AssistantToolResult>.Success(new(tool, "success", rows));
        }

        if (input.From is null || input.To is null || input.To < input.From ||
            string.IsNullOrWhiteSpace(input.TimeZone))
            return Fail("assistant.invalid_tool_input", "A valid first date, last date, and time zone are required.");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(input.TimeZone); }
        catch (TimeZoneNotFoundException) { return Fail("assistant.invalid_tool_input", "The time zone is not supported."); }
        catch (InvalidTimeZoneException) { return Fail("assistant.invalid_tool_input", "The time zone is not supported."); }

        if (tool != AssistantTools.BestSellers && (input.SortBy is not null || input.Limit is not null))
            return Fail("assistant.invalid_tool_input", "This report does not support ranking parameters.");
        if (tool == AssistantTools.BestSellers &&
            (input.SortBy is not (null or "quantity" or "revenue") || input.Limit is < 1 or > 50))
            return Fail("assistant.invalid_tool_input", "Use quantity or revenue ranking and a limit from 1 to 50.");

        if (tool == AssistantTools.Revenue)
        {
            var result = await revenue.GetRevenueAsync(new(input.From.Value, input.To.Value, zone), cancellationToken);
            if (result.IsFailure) return Result<AssistantToolResult>.Failure(result.Error!);
            var value = result.Value;
            return Result<AssistantToolResult>.Success(new(tool, "success", new RevenueReportResponse(
                value.FirstDate, value.LastDateInclusive, value.TimeZoneId, value.StartUtc, value.EndUtcExclusive,
                value.Revenue, value.OrderCount,
                value.Daily.Select(row => new RevenueDayResponse(row.Date, row.Revenue, row.OrderCount)).ToArray(),
                value.ByPaymentMethod.Select(row => new PaymentRevenueResponse(row.PaymentMethod, row.Revenue, row.OrderCount)).ToArray())));
        }
        if (tool == AssistantTools.Channels)
        {
            var result = await channels.GetChannelsAsync(new(input.From.Value, input.To.Value, zone), cancellationToken);
            if (result.IsFailure) return Result<AssistantToolResult>.Failure(result.Error!);
            var value = result.Value;
            return Result<AssistantToolResult>.Success(new(tool, "success", new ChannelReportResponse(
                value.FirstDate, value.LastDateInclusive, value.TimeZoneId, value.StartUtc, value.EndUtcExclusive,
                value.Revenue, value.OrderCount,
                value.ByChannel.Select(row => new ChannelRevenueResponse(row.Channel, row.Revenue, row.OrderCount)).ToArray())));
        }
        var sort = input.SortBy == "revenue" ? BestSellerSort.Revenue : BestSellerSort.Quantity;
        var sellers = await bestSellers.GetBestSellersAsync(new(input.From.Value, input.To.Value, zone,
            sort, input.Limit ?? 10), cancellationToken);
        if (sellers.IsFailure) return Result<AssistantToolResult>.Failure(sellers.Error!);
        var report = sellers.Value;
        return Result<AssistantToolResult>.Success(new(tool, "success", new BestSellerReportResponse(
            report.FirstDate, report.LastDateInclusive, report.TimeZoneId, report.StartUtc, report.EndUtcExclusive,
            report.SortBy.ToString().ToLowerInvariant(), report.Items.Select(ToRow).ToArray(),
            report.Variants.Select(ToRow).ToArray())));
    }

    private static BestSellerRowResponse ToRow(BestSellerRow row) => new(row.Rank, row.MenuItemId?.Value,
        row.SizeVariantId?.Value, row.ItemName, row.SizeName, row.Quantity, row.Revenue);

    private static Result<AssistantToolResult> Fail(string code, string message) =>
        Result<AssistantToolResult>.Failure(Error.Validation(code, message));
}
