using System.Net;
using System.Net.Http.Json;
using CoffeeShop.Contracts;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;

namespace CoffeeShop.WinUI.Tests;

public sealed class ReportsApiTests
{
    [Fact]
    public async Task ReportsWorkspace_UsesAuthenticatedHttpAndKeepsForbiddenRegionSeparate()
    {
        var session = new StaffSessionManager(new Identity(), new Store());
        await session.LoginAsync("fixture", "fixture");
        var requests = new List<(string Path, string? Bearer)>();
        using var http = new HttpClient(new Handler(request =>
        {
            requests.Add((request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter));
            if (request.RequestUri.AbsolutePath.EndsWith("/revenue"))
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
            var end = start.AddDays(1);
            object payload = request.RequestUri.AbsolutePath switch
            {
                "/api/v1/reports/best-sellers" => new BestSellerReportResponse(new(2026, 10, 1), new(2026, 10, 1),
                    "UTC", start, end, "quantity", [new(1, null, null, "Tea", null, 2, 12m)], []),
                "/api/v1/reports/channels" => new ChannelReportResponse(new(2026, 10, 1), new(2026, 10, 1),
                    "UTC", start, end, 12m, 1, [new("takeaway", 12m, 1)]),
                "/api/v1/reports/low-stock" => new[] { new LowStockReportRowResponse(Guid.NewGuid(), "Milk", "ml", 1, 2) },
                _ => throw new InvalidOperationException("Unexpected HTTP route."),
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };
        }))
        { BaseAddress = new Uri("https://example.test/api/v1/") };
        var model = new ReportsWorkspace(new ApiReportsSource(new AuthenticatedApiClient(http, session)));
        var day = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

        await model.LoadAsync(day, day, TimeZoneInfo.Utc);

        Assert.Equal("reports.forbidden", model.Revenue.Error!.Code);
        Assert.Null(model.Revenue.Value);
        Assert.Equal("Tea", Assert.Single(model.BestSellers.Value!.Items).ItemName);
        Assert.Equal(12m, model.Channels.Value!.Revenue);
        Assert.Equal("Milk", Assert.Single(model.LowStock.Value!).Name);
        Assert.Equal(4, requests.Count);
        Assert.All(requests, request => Assert.Equal("fixture-access", request.Bearer));
        Assert.Contains("from=2026-10-01&to=2026-10-01&timeZone=UTC", requests[0].Path);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class Identity : IStaffIdentityApi
    {
        public Task<TokenResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TokenResponse("fixture-access", "fixture-refresh", DateTimeOffset.UtcNow.AddHours(1),
                new StaffSession(Guid.NewGuid(), "Fixture", [], [])));
        public Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task LogoutAsync(string refreshToken, string accessToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Store : IRefreshTokenStore
    {
        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task SaveAsync(string refreshToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
