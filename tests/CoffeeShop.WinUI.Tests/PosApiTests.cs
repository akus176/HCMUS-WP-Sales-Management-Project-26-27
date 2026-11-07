using System.Net;
using System.Net.Http.Json;
using CoffeeShop.Contracts;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.Tests;

public sealed class PosApiTests
{
    [Fact]
    public async Task DraftAndConfirmation_UseAuthenticatedHttpWithoutDirectHandlers()
    {
        var categoryId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var user = new StaffSession(Guid.NewGuid(), "Test cashier", [], ["orders.create", "orders.confirm"]);
        var session = new StaffSessionManager(new Identity(user), new Store());
        await session.LoginAsync("fixture", "fixture");
        var calls = new List<(string Method, string Path, string? Bearer)>();
        Guid orderId = default;
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            calls.Add((request.Method.Method, path, request.Headers.Authorization?.Parameter));
            object response = path switch
            {
                "/api/v1/menu-items" => new[] { new MenuItemResponse(itemId, categoryId, "Drinks", "Tea", null, true,
                    [new(variantId, "Small", 5m, true)], []) },
                "/api/v1/categories" => new[] { new CategoryResponse(categoryId, "Drinks", 0, true) },
                "/api/v1/pos/tables" => Array.Empty<PosTableResponse>(),
                "/api/v1/orders" => Draft((await request.Content!.ReadFromJsonAsync<CreateOrderRequest>())!.OrderId, "draft"),
                _ when path.EndsWith("/confirm") => Draft(orderId, "confirmed"),
                _ => throw new InvalidOperationException($"Unexpected route {path}"),
            };
            if (response is OrderResponse order) orderId = order.Id;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
        })) { BaseAddress = new Uri("https://example.test/api/v1/") };
        using var services = new ServiceCollection().BuildServiceProvider();
        var actor = new PosOperatorSession(() => session.CurrentUser);
        var model = new PosWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new CustomerOrderContext(), actor,
            new ApiPosSource(new AuthenticatedApiClient(http, session), session));

        await model.LoadAsync();
        var item = Assert.Single(model.Menu);
        model.AddLine(item, Assert.Single(item.SizeVariants), 1, []);
        await model.SaveDraftAsync();
        Assert.Equal("draft", model.Order!.Status);
        await model.ConfirmAsync();

        Assert.Equal("confirmed", model.Order!.Status);
        Assert.Equal(user.Id, model.Session.Id!.Value);
        Assert.All(calls, call => Assert.Equal("fixture-access", call.Bearer));
        Assert.Contains(calls, call => call.Method == "POST" && call.Path.EndsWith("/confirm"));
    }

    private static OrderResponse Draft(Guid id, string status) => new(id, "POS-1", "takeaway", status,
        null, null, null, 5m, 0m, 5m, [new("Tea", "Small", 1, 5m, 5m, [], [])], []);

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
    private sealed class Identity(StaffSession user) : IStaffIdentityApi
    {
        public Task<TokenResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TokenResponse("fixture-access", "fixture-refresh", DateTimeOffset.UtcNow.AddHours(1), user));
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
