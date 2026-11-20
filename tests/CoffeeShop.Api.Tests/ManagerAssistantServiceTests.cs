using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Tests;

public sealed class ManagerAssistantServiceTests
{
    [Theory]
    [InlineData("Please cancel order 5", "refused")]
    [InlineData("What is the weather?", "unsupported")]
    public async Task RefusalAndUnsupportedQuestionsInvokeNoTool(string question, string outcome)
    {
        await using var db = Database();
        var gateway = new StubGateway(null);
        var service = Create(db, gateway);
        var response = await service.AskAsync(Request(question));
        Assert.True(response.IsSuccess);
        Assert.Empty(response.Value.ToolsInvoked);
        Assert.Equal(0, gateway.Calls);
        Assert.Equal(outcome, (await db.AiInteractionAudits.SingleAsync()).Outcome);
    }

    [Fact]
    public async Task EmptyReportIsExplicitlyEmptyAndAudited()
    {
        await using var db = Database();
        var gateway = new StubGateway(new AssistantToolResult(AssistantTools.Revenue, "success",
            new RevenueReportResponse(new(2026, 11, 1), new(2026, 11, 15), "UTC",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0, 0, [], [])));
        var response = await Create(db, gateway).AskAsync(Request("Show revenue"));
        Assert.True(response.IsSuccess);
        Assert.Contains("No matching revenue data", response.Value.Answer);
        Assert.Single(response.Value.ToolsInvoked);
        var audit = await db.AiInteractionAudits.SingleAsync();
        Assert.Equal("success", audit.Outcome);
        Assert.Contains("revenue:success", audit.Tools);
        Assert.True(audit.DurationMs >= 0);
    }

    [Fact]
    public async Task MissingPermissionDoesNotRevealReportMetrics()
    {
        await using var db = Database();
        var gateway = new StubGateway(null, denied: true);
        var result = await Create(db, gateway).AskAsync(Request("Show revenue"));
        Assert.True(result.IsFailure);
        Assert.Equal("assistant.forbidden", result.Error?.Code);
        var audit = await db.AiInteractionAudits.SingleAsync();
        Assert.Equal("denied", audit.Outcome);
        Assert.Equal("revenue:denied", audit.Tools);
    }

    [Fact]
    public async Task NonemptyAnswerUsesOnlyReturnedRevenueAndReportingPeriod()
    {
        await using var db = Database();
        var report = new RevenueReportResponse(new(2026, 11, 1), new(2026, 11, 15), "UTC",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 123m, 7, [], []);
        var gateway = new StubGateway(new AssistantToolResult(AssistantTools.Revenue, "success", report));

        var result = await Create(db, gateway).AskAsync(Request("Show revenue"));

        Assert.True(result.IsSuccess);
        Assert.Contains("From 2026-11-01 through 2026-11-15", result.Value.Answer);
        Assert.Contains($"{report.Revenue:N2} from {report.OrderCount} orders", result.Value.Answer);
        Assert.Same(report, Assert.Single(result.Value.ToolsInvoked).Metrics);
        Assert.Equal(1, gateway.Calls);
        Assert.Equal("revenue:success", (await db.AiInteractionAudits.SingleAsync()).Tools);
    }

    [Fact]
    public async Task ComparisonUsesBothPeriodsAndCombinesReportsWithoutRefusingTheWordChange()
    {
        await using var db = Database();
        var currentFrom = new DateOnly(2026, 11, 1);
        var currentTo = new DateOnly(2026, 11, 15);
        var previousFrom = new DateOnly(2026, 10, 17);
        var previousTo = new DateOnly(2026, 10, 31);
        var gateway = new StubGateway(null, getResult: (tool, input) => tool switch
        {
            AssistantTools.Revenue => new(tool, "success", MakeRevenue(input)),
            AssistantTools.LowStock => new(tool, "success", new LowStockReportRowResponse[]
            {
                new(Guid.NewGuid(), "Oat milk", "ml", 100m, 250m),
            }),
            _ => throw new InvalidOperationException("Unexpected report."),
        });

        var result = await Create(db, gateway).AskAsync(Request(
            "How did revenue and average order value change compared with the previous period, and which ingredients are low in stock?"));

        Assert.True(result.IsSuccess);
        Assert.Collection(gateway.Requests,
            entry => Assert.Equal((AssistantTools.Revenue, new AssistantToolInput(currentFrom, currentTo, "UTC")), entry),
            entry => Assert.Equal((AssistantTools.Revenue, new AssistantToolInput(previousFrom, previousTo, "UTC")), entry),
            entry => Assert.Equal((AssistantTools.LowStock, new AssistantToolInput()), entry));
        Assert.Contains("2026-10-17 to 2026-10-31", result.Value.Answer);
        Assert.Contains($"revenue {150m:+#,##0.00;-#,##0.00;0.00} ({.5m:P1})", result.Value.Answer);
        Assert.Contains("Oat milk", result.Value.Answer);
        Assert.Contains($"{150m:N3} ml below threshold", result.Value.Answer);
        Assert.Equal("revenue:success,revenue:success,low_stock:success", (await db.AiInteractionAudits.SingleAsync()).Tools);

        RevenueReportResponse MakeRevenue(AssistantToolInput input)
        {
            var current = input.From == currentFrom;
            return new(input.From!.Value, input.To!.Value, "UTC", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                current ? 450m : 300m, current ? 10 : 8, [], []);
        }
    }

    [Fact]
    public async Task OpenEndedQuestionDoesNotSilentlyUseSelectedDates()
    {
        await using var db = Database();
        var gateway = new StubGateway(null);
        var response = await Create(db, gateway).AskAsync(Request("Which products sold best all time?"));
        Assert.True(response.IsSuccess);
        Assert.Contains("Choose a start and end date", response.Value.Answer);
        Assert.Empty(gateway.Requests);
        Assert.Equal("needs_dates", (await db.AiInteractionAudits.SingleAsync()).Outcome);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpansionRequestReturnsAnActionPlanWithOnlyAvailableEvidence(bool hasSales)
    {
        await using var db = Database();
        var gateway = new StubGateway(null, getResult: (tool, input) =>
        {
            var start = DateTimeOffset.UtcNow;
            object metrics = tool switch
            {
                AssistantTools.Revenue => new RevenueReportResponse(input.From!.Value, input.To!.Value, "UTC",
                    start, start, hasSales ? 450m : 0m, hasSales ? 10 : 0, [], []),
                AssistantTools.BestSellers => new BestSellerReportResponse(input.From!.Value, input.To!.Value, "UTC",
                    start, start, "quantity", hasSales ? [new(1, null, null, "Oat Latte", null, 4, 80m)] : [], []),
                AssistantTools.Channels => new ChannelReportResponse(input.From!.Value, input.To!.Value, "UTC",
                    start, start, hasSales ? 450m : 0m, hasSales ? 10 : 0, hasSales ? [new("takeaway", 450m, 10)] : []),
                AssistantTools.LowStock => new LowStockReportRowResponse[] { new(Guid.NewGuid(), "Vanilla syrup", "ml", 4m, 150m) },
                _ => throw new InvalidOperationException("Unexpected report."),
            };
            return new(tool, "success", metrics);
        });

        var result = await Create(db, gateway).AskAsync(Request("Give me the plan to expand my business"));

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.ToolsInvoked.Count);
        Assert.Contains("Four-week café expansion plan", result.Value.Answer);
        Assert.Contains("Vanilla syrup", result.Value.Answer);
        Assert.Contains("Week 4", result.Value.Answer);
        if (hasSales)
        {
            Assert.Contains("Oat Latte", result.Value.Answer);
            Assert.Contains("Takeaway", result.Value.Answer);
        }
        else
        {
            Assert.Contains("no recognized sales", result.Value.Answer);
            Assert.DoesNotContain("Oat Latte", result.Value.Answer);
        }
    }

    [Fact]
    public async Task AuditRedactsCredentialsInQuestion()
    {
        await using var db = Database();
        var service = Create(db, new StubGateway(null));
        await service.AskAsync(Request("Weather? token=abc123 password=hunter2 Bearer xyz123 sk-abcdefgh nvapi-fictional-demo eyJabc.eyJdef.signature"));
        var question = (await db.AiInteractionAudits.SingleAsync()).Question;
        Assert.DoesNotContain("abc123", question);
        Assert.DoesNotContain("hunter2", question);
        Assert.DoesNotContain("xyz123", question);
        Assert.DoesNotContain("sk-abcdefgh", question);
        Assert.DoesNotContain("nvapi-fictional-demo", question);
        Assert.DoesNotContain("eyJabc.eyJdef.signature", question);
        Assert.Contains("[REDACTED]", question);
    }

    private static AssistantAskRequest Request(string question) =>
        new(question, new(2026, 11, 1), new(2026, 11, 15), "UTC");

    private static CoffeeShopDbContext Database() => new(new DbContextOptionsBuilder<CoffeeShopDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ManagerAssistantService Create(CoffeeShopDbContext db, IAssistantToolGateway gateway) =>
        new(new ManagerUser(), new DeterministicAssistantProvider(), gateway, db, TimeProvider.System);

    private sealed class StubGateway(AssistantToolResult? result, bool denied = false,
        Func<string, AssistantToolInput, AssistantToolResult>? getResult = null) : IAssistantToolGateway
    {
        public List<(string Tool, AssistantToolInput Input)> Requests { get; } = [];
        public int Calls => Requests.Count;
        public Task<Result<AssistantToolResult>> InvokeAsync(string tool, AssistantToolInput input,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((tool, input));
            return Task.FromResult(denied
                ? Result<AssistantToolResult>.Failure(Error.Authorization("assistant.forbidden", "Forbidden"))
                : Result<AssistantToolResult>.Success(getResult?.Invoke(tool, input) ?? result!));
        }
    }

    private sealed class ManagerUser : ICurrentUser
    {
        public EntityId? Id { get; } = EntityId.New();
        public string? Username => "manager";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => ["Manager"];
        public IReadOnlyCollection<string> Permissions =>
            ["assistant.ask", "reports.revenue", "reports.bestsellers", "reports.channels", "reports.lowstock"];
        public bool HasPermission(string permission) => Permissions.Contains(permission);
        public bool IsInRole(string role) => Roles.Contains(role);
        public EntityId GetRequiredId() => Id!;
    }
}
