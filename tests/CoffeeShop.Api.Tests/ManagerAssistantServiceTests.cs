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
    public async Task AuditRedactsCredentialsInQuestion()
    {
        await using var db = Database();
        var service = Create(db, new StubGateway(null));
        await service.AskAsync(Request("Weather? token=abc123 password=hunter2 Bearer xyz123 sk-abcdefgh eyJabc.eyJdef.signature"));
        var question = (await db.AiInteractionAudits.SingleAsync()).Question;
        Assert.DoesNotContain("abc123", question);
        Assert.DoesNotContain("hunter2", question);
        Assert.DoesNotContain("xyz123", question);
        Assert.DoesNotContain("sk-abcdefgh", question);
        Assert.DoesNotContain("eyJabc.eyJdef.signature", question);
        Assert.Contains("[REDACTED]", question);
    }

    private static AssistantAskRequest Request(string question) =>
        new(question, new(2026, 11, 1), new(2026, 11, 15), "UTC");

    private static CoffeeShopDbContext Database() => new(new DbContextOptionsBuilder<CoffeeShopDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ManagerAssistantService Create(CoffeeShopDbContext db, IAssistantToolGateway gateway) =>
        new(new ManagerUser(), new DeterministicAssistantProvider(), gateway, db, TimeProvider.System);

    private sealed class StubGateway(AssistantToolResult? result, bool denied = false) : IAssistantToolGateway
    {
        public int Calls { get; private set; }
        public Task<Result<AssistantToolResult>> InvokeAsync(string tool, AssistantToolInput input,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(denied
                ? Result<AssistantToolResult>.Failure(Error.Authorization("assistant.forbidden", "Forbidden"))
                : Result<AssistantToolResult>.Success(result!));
        }
    }

    private sealed class ManagerUser : ICurrentUser
    {
        public EntityId? Id { get; } = EntityId.New();
        public string? Username => "manager";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => ["Manager"];
        public IReadOnlyCollection<string> Permissions => ["assistant.ask", "reports.revenue"];
        public bool HasPermission(string permission) => Permissions.Contains(permission);
        public bool IsInRole(string role) => Roles.Contains(role);
        public EntityId GetRequiredId() => Id!;
    }
}
