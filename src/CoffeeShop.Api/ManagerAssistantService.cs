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
    private static readonly AssistantReportSelection[] ExpansionReports =
    [
        new(AssistantTools.Revenue),
        new(AssistantTools.BestSellers, SortBy: "quantity", Limit: 3),
        new(AssistantTools.Channels),
        new(AssistantTools.LowStock),
    ];

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
                return Reply("I can read reports. Changes to business data must be made in the relevant workspace.");
            }

            AssistantReportPlan plan;
            try
            {
                plan = await provider.PlanAsync(request with { Question = Redact(question) }, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                exception is HttpRequestException or InvalidOperationException or System.Text.Json.JsonException or TaskCanceledException)
            {
                outcome = "provider_unavailable";
                return ProviderUnavailable();
            }

            switch (plan.Status)
            {
                case "refused":
                    outcome = "refused";
                    return Reply("I can read reports. Changes to business data must be made in the relevant workspace.");
                case "needs_dates":
                    outcome = "needs_dates";
                    return Reply("Choose a start and end date using Change dates, then ask again about the selected period. To compare periods, you can ask about the preceding period of the same length.");
                case "unsupported":
                    outcome = "unsupported";
                    return Reply("I can answer questions about recognized revenue, best sellers, order channels, and current low stock.");
            }

            var expansion = plan.Status == AssistantReportPlan.Expansion;
            if (expansion)
                plan = plan with { Reports = ExpansionReports, ComparePrevious = false };

            if ((!expansion && plan.Status is not ("answer" or "partial")) || !IsValidPlan(plan))
            {
                outcome = "provider_unavailable";
                return ProviderUnavailable();
            }

            var from = plan.From ?? request.From;
            var to = plan.To ?? request.To;
            DateOnly? previousFrom = null;
            DateOnly? previousTo = null;
            if (plan.ComparePrevious && plan.Reports.Any(report => report.Tool != AssistantTools.LowStock))
            {
                if (from is null || to is null || to.Value < from.Value ||
                    from.Value.DayNumber < to.Value.DayNumber - from.Value.DayNumber + 1)
                    return Fail("assistant.invalid_period", "Choose a valid reporting period with enough dates to compare it with the preceding period.");
                var days = to.Value.DayNumber - from.Value.DayNumber + 1;
                previousFrom = from.Value.AddDays(-days);
                previousTo = from.Value.AddDays(-1);
            }

            var answer = new List<string>();
            Error? deniedReport = null;
            foreach (var report in plan.Reports)
            {
                var input = report.Tool == AssistantTools.LowStock ? new AssistantToolInput() :
                    new AssistantToolInput(from, to, request.TimeZone, report.SortBy, report.Limit);
                var current = await QueryAsync(report.Tool, input);
                if (current.IsFailure)
                {
                    if (!expansion || current.Error?.Code != "assistant.forbidden")
                        return Result<AssistantAskResponse>.Failure(current.Error!);
                    deniedReport = current.Error;
                    continue;
                }
                if (expansion) continue;

                AssistantToolResult? prior = null;
                if (previousFrom is not null && report.Tool != AssistantTools.LowStock)
                {
                    var previous = await QueryAsync(report.Tool, input with { From = previousFrom, To = previousTo });
                    if (previous.IsFailure) return Result<AssistantAskResponse>.Failure(previous.Error!);
                    prior = previous.Value;
                }
                answer.Add(AssistantReportAnswer.Describe(current.Value, prior, report.View));
            }

            if (expansion)
            {
                var available = tools.Where(result => result.Outcome == "success").ToArray();
                if (available.Length == 0)
                    return Result<AssistantAskResponse>.Failure(deniedReport!);
                outcome = "success";
                return Result<AssistantAskResponse>.Success(new(
                    AssistantReportAnswer.ExpansionPlan(available, from, to), from, to, available));
            }

            if (plan.Status == "partial")
                answer.Add("The available reports cover the figures above. Other parts of the question require additional data.");
            outcome = "success";
            return Result<AssistantAskResponse>.Success(new(string.Join("\n\n", answer), from, to, tools));

            async Task<Result<AssistantToolResult>> QueryAsync(string name, AssistantToolInput input)
            {
                var result = await gateway.InvokeAsync(name, input, cancellationToken);
                if (result.IsSuccess)
                {
                    tools.Add(result.Value);
                }
                else
                {
                    outcome = result.Error?.Code == "assistant.forbidden" ? "denied" : "tool_error";
                    tools.Add(new(name, outcome, null));
                }
                return result;
            }

            Result<AssistantAskResponse> Reply(string answer) =>
                Result<AssistantAskResponse>.Success(new(answer, request.From, request.To, tools));
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

    private static bool IsValidPlan(AssistantReportPlan plan)
    {
        if (plan.Reports is not { Length: > 0 and <= 5 } ||
            (plan.From is null) != (plan.To is null) || plan.From > plan.To ||
            plan.Reports.Any(report => !IsValidSelection(report)))
            return false;
        if (plan.Reports.Distinct().Count() != plan.Reports.Length) return false;
        var comparisons = plan.ComparePrevious ? plan.Reports.Count(report => report.Tool != AssistantTools.LowStock) : 0;
        return plan.Reports.Length + comparisons <= 8;
    }

    private static bool IsValidSelection(AssistantReportSelection? report) => report switch
    {
        { Tool: AssistantTools.Revenue, SortBy: null, Limit: null } => report.View is null or "daily" or "payments" or "daily_and_payments",
        { Tool: AssistantTools.BestSellers } => (report.View is null or "variants")
            && (report.SortBy is null or "quantity" or "revenue") && report.Limit is null or (>= 1 and <= 50),
        { Tool: AssistantTools.Channels or AssistantTools.LowStock, View: null, SortBy: null, Limit: null } => true,
        _ => false,
    };

    private static Result<AssistantAskResponse> ProviderUnavailable() =>
        Result<AssistantAskResponse>.Failure(Error.Unexpected("assistant.provider_unavailable",
            "The assistant provider could not plan the requested reports. Please try again."));

    private static Result<AssistantAskResponse> Fail(string code, string message) =>
        Result<AssistantAskResponse>.Failure(Error.Validation(code, message));

    [GeneratedRegex(@"(?:\A|[.!?;\n]\s*|\b(?:and|then)\s+)(?:(?:please|(?:can|could|would|will)\s+you|i\s+want\s+you\s+to)\s+)*(?:create|update|delete|remove|approve|cancel|change|modify|edit|place|refund|assign|adjust|mark|set|add|save|reset|transfer|deactivate|reactivate|pay|complete|confirm|fulfill|receive)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MutationRequest();
    [GeneratedRegex(@"\b((?:password|passwd|api[_ -]?key|secret|token)\s*(?::|=|is)\s*)\S+", RegexOptions.IgnoreCase)]
    private static partial Regex SecretPattern();
    [GeneratedRegex(@"Bearer\s+\S+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerPattern();
    [GeneratedRegex(@"\b(?:sk-|nvapi-)[A-Za-z0-9_-]{8,}\b")]
    private static partial Regex KeyPattern();
    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]+\.eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b")]
    private static partial Regex JwtPattern();
}
