namespace CoffeeShop.Api;

using System.Diagnostics;
using System.Text.RegularExpressions;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Reporting;
using CoffeeShop.Infrastructure.Postgres;

public sealed partial class ManagerAssistantService(
    ICurrentUser user, IManagerAssistantProvider provider, IAssistantToolGateway gateway,
    CoffeeShopDbContext db, TimeProvider time)
{
    public async Task<Result<AssistantAskResponse>> AskAsync(AssistantAskRequest request,
        CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        var tools = new List<AssistantToolResult>();
        var outcome = "validation_error";
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 2000)
                return Fail("assistant.invalid_question", "Ask a question of at most 2,000 characters.");
            var question = request.Question.Trim();
            if (!user.IsAuthenticated || !(user.IsInRole("Manager") || user.IsInRole("Admin")) ||
                !user.HasPermission("assistant.ask"))
            {
                outcome = "denied";
                return Result<AssistantAskResponse>.Failure(Error.Authorization(
                    "assistant.forbidden", "Only an authorized manager can use the assistant."));
            }
            if (MutationRequest().IsMatch(question))
            {
                outcome = "refused";
                return Result<AssistantAskResponse>.Success(new(
                    "I can only read reports. I cannot create, update, delete, approve, cancel, or change business data.",
                    request.From, request.To, tools));
            }
            string? tool;
            try { tool = await provider.SelectToolAsync(question, cancellationToken); }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or
                System.Text.Json.JsonException or TaskCanceledException)
            {
                outcome = "provider_unavailable";
                return Result<AssistantAskResponse>.Failure(Error.Unexpected("assistant.provider_unavailable",
                    "The assistant provider is unavailable. Check its configuration and try again."));
            }
            if (tool is not (AssistantTools.Revenue or AssistantTools.BestSellers or
                AssistantTools.Channels or AssistantTools.LowStock))
            {
                outcome = "unsupported";
                return Result<AssistantAskResponse>.Success(new(
                    "I can answer questions about revenue, best sellers, order channels, and current low stock only.",
                    request.From, request.To, tools));
            }
            var input = tool == AssistantTools.LowStock ? new AssistantToolInput() :
                new AssistantToolInput(request.From, request.To, request.TimeZone);
            var result = await gateway.InvokeAsync(tool, input, cancellationToken);
            if (result.IsFailure)
            {
                outcome = result.Error?.Code == "assistant.forbidden" ? "denied" : "tool_error";
                tools.Add(new(tool, outcome, null));
                return Result<AssistantAskResponse>.Failure(result.Error!);
            }
            tools.Add(result.Value);
            outcome = "success";
            return Result<AssistantAskResponse>.Success(new(
                GroundedAnswer(result.Value, request.From, request.To), request.From, request.To, tools));
        }
        finally
        {
            if (user.IsAuthenticated && user.Id is not null)
            {
                var sanitized = Redact(request?.Question ?? string.Empty);
                db.AiInteractionAudits.Add(new AiInteractionAudit(EntityId.New(), user.GetRequiredId(),
                    new UtcTimestamp(time.GetUtcNow()), sanitized,
                    string.Join(',', tools.Select(item => $"{item.Tool}:{item.Outcome}")), outcome, timer.ElapsedMilliseconds));
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    public static string Redact(string question)
    {
        var clean = SecretPattern().Replace(question, "$1[REDACTED]");
        clean = BearerPattern().Replace(clean, "Bearer [REDACTED]");
        clean = KeyPattern().Replace(clean, "[REDACTED]");
        clean = JwtPattern().Replace(clean, "[REDACTED]");
        return clean.Length > 2000 ? clean[..2000] : clean;
    }

    private static string GroundedAnswer(AssistantToolResult result, DateOnly? from, DateOnly? to)
    {
        var period = from is not null && to is not null ? $"From {from:yyyy-MM-dd} through {to:yyyy-MM-dd}: " : "Current stock: ";
        return result.Metrics switch
        {
            RevenueReportResponse revenue when revenue.OrderCount == 0 => period + "No matching revenue data was found.",
            RevenueReportResponse revenue => period +
                $"recognized revenue is {revenue.Revenue:N2} from {revenue.OrderCount} orders.",
            BestSellerReportResponse sellers when sellers.Items.Count == 0 => period + "No matching best-seller data was found.",
            BestSellerReportResponse sellers => period + "best sellers are " +
                string.Join("; ", sellers.Items.Take(5).Select(item =>
                    $"{item.ItemName} ({item.Quantity} sold, {item.Revenue:N2} revenue)")) + ".",
            ChannelReportResponse channels when channels.OrderCount == 0 => period + "No matching order-channel data was found.",
            ChannelReportResponse channels => period + "channel results: " +
                string.Join("; ", channels.ByChannel.Select(row =>
                    $"{row.Channel} ({row.OrderCount} orders, {row.Revenue:N2} revenue)")) + ".",
            LowStockReportRowResponse[] { Length: 0 } => "Current stock: no active ingredients are at or below their reorder threshold.",
            LowStockReportRowResponse[] stock => "Current low-stock ingredients: " +
                string.Join("; ", stock.Take(10).Select(row =>
                    $"{row.Name} ({row.AvailableQuantity:N2} {row.BaseUnit}, threshold {row.ReorderThreshold:N2})")) + ".",
            _ => "No matching report data was found.",
        };
    }

    private static Result<AssistantAskResponse> Fail(string code, string message) =>
        Result<AssistantAskResponse>.Failure(Error.Validation(code, message));

    [GeneratedRegex(@"\b(create|update|delete|remove|approve|cancel|change|modify|edit|place|refund|assign|adjust|mark|set|add|save|reset|transfer|deactivate|reactivate|pay|complete|confirm|fulfill|receive)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MutationRequest();
    [GeneratedRegex(@"\b((?:password|passwd|api[_ -]?key|secret|token)\s*(?::|=|is)\s*)\S+", RegexOptions.IgnoreCase)]
    private static partial Regex SecretPattern();
    [GeneratedRegex(@"Bearer\s+\S+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerPattern();
    [GeneratedRegex(@"\bsk-[A-Za-z0-9_-]{8,}\b")]
    private static partial Regex KeyPattern();
    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]+\.eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b")]
    private static partial Regex JwtPattern();
}
