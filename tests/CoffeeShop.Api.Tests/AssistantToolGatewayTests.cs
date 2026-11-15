using System.Text.Json;
using CoffeeShop.Api;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;

namespace CoffeeShop.Api.Tests;

public sealed class AssistantToolGatewayTests
{
    [Theory]
    [InlineData("revenue", "reports.revenue")]
    [InlineData("best_sellers", "reports.bestsellers")]
    [InlineData("order_channels", "reports.channels")]
    [InlineData("low_stock", "reports.lowstock")]
    public async Task MissingReportPermissionReturnsNoMetrics(string tool, string permission)
    {
        var user = new StubUser(["assistant.ask"], ["Manager"]);
        var result = await Gateway(user).InvokeAsync(tool, new());
        Assert.True(result.IsFailure);
        Assert.Equal("assistant.forbidden", result.Error?.Code);
        Assert.DoesNotContain(permission, user.Permissions);
    }

    [Fact]
    public async Task NonManagerCannotUseToolDespitePermission()
    {
        var result = await Gateway(new StubUser(["assistant.ask", "reports.revenue"], ["Barista"]))
            .InvokeAsync(AssistantTools.Revenue, new());
        Assert.True(result.IsFailure);
        Assert.Equal("assistant.forbidden", result.Error?.Code);
    }

    [Fact]
    public async Task InvalidAndUnsupportedInputsFailBeforeQuery()
    {
        var gateway = Gateway(new StubUser(["assistant.ask", "reports.revenue", "reports.lowstock"], ["Manager"]));
        var reversed = await gateway.InvokeAsync(AssistantTools.Revenue,
            new(new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 1), "UTC"));
        var unsupported = await gateway.InvokeAsync(AssistantTools.Revenue,
            new(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 15), "UTC", Limit: 2));
        var lowStockPeriod = await gateway.InvokeAsync(AssistantTools.LowStock,
            new(new DateOnly(2026, 11, 1)));
        Assert.All(new[] { reversed, unsupported, lowStockPeriod }, result =>
        {
            Assert.True(result.IsFailure);
            Assert.Equal("assistant.invalid_tool_input", result.Error?.Code);
        });
    }

    [Fact]
    public void ToolInputRejectsUnexpectedJsonParameters() =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AssistantToolInput>(
            "{\"from\":\"2026-11-01\",\"to\":\"2026-11-15\",\"sql\":\"select * from users\"}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private static AssistantToolGateway Gateway(ICurrentUser user) => new(user, null!, null!, null!, null!);

    private sealed class StubUser(IReadOnlyCollection<string> permissions, IReadOnlyCollection<string> roles) : ICurrentUser
    {
        public EntityId? Id { get; } = EntityId.New();
        public string? Username => "manager";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => roles;
        public IReadOnlyCollection<string> Permissions => permissions;
        public bool HasPermission(string permission) => permissions.Contains(permission);
        public bool IsInRole(string role) => roles.Contains(role);
        public EntityId GetRequiredId() => Id!;
    }
}
