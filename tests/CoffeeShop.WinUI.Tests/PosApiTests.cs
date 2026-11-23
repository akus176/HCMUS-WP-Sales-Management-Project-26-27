using System.Net;
using System.Net.Http.Json;
using CoffeeShop.Application.Customers;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.Tests;

public sealed class PosApiTests
{
    [Fact]
    public async Task ManagerCreatesTable_AndCounterRefreshesAvailableSelection()
    {
        var user = new StaffSession(Guid.NewGuid(), "Test manager", ["Manager"], ["orders.create", "tables.manage"]);
        var session = new StaffSessionManager(new Identity(user), new Store());
        await session.LoginAsync("fixture", "fixture");
        var tableId = Guid.NewGuid();
        var tables = new List<PosTableResponse>();
        var posts = new List<(string? Bearer, CreateTableRequest Body)>();
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/v1/tables")
            {
                var body = (await request.Content!.ReadFromJsonAsync<CreateTableRequest>())!;
                posts.Add((request.Headers.Authorization?.Parameter, body));
                var row = new PosTableResponse(tableId, body.TableNumber!, body.Seats, false, null);
                tables.Add(row);
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(row) };
            }
            object result = path switch
            {
                "/api/v1/menu-items" => Array.Empty<MenuItemResponse>(),
                "/api/v1/categories" => Array.Empty<CategoryResponse>(),
                "/api/v1/pos/tables" => tables.ToArray(),
                _ => throw new InvalidOperationException($"Unexpected route {path}"),
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(result) };
        })) { BaseAddress = new Uri("https://example.test/api/v1/") };
        using var services = new ServiceCollection().BuildServiceProvider();
        var actor = new PosOperatorSession(() => session.CurrentUser);
        var model = new PosWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new CustomerOrderContext(), actor,
            new ApiPosSource(new AuthenticatedApiClient(http, session), session));

        await model.LoadAsync();
        Assert.True(model.CanManageTables);
        await model.CreateTableAsync(" T12 ", 4);

        Assert.Null(model.Error);
        Assert.Equal("T12", Assert.Single(model.Options.Tables).TableNumber);
        Assert.Equal(tableId, model.TableId!.Value);
        Assert.Equal("fixture-access", Assert.Single(posts).Bearer);
    }

    [Fact]
    public async Task CashierCannotCreateTableFromCounter()
    {
        var user = new StaffSession(Guid.NewGuid(), "Test cashier", ["Cashier"], ["orders.create"]);
        var session = new StaffSessionManager(new Identity(user), new Store());
        await session.LoginAsync("fixture", "fixture");
        using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("No table write expected")))
            { BaseAddress = new Uri("https://example.test/api/v1/") };
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new PosWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new CustomerOrderContext(),
            new PosOperatorSession(() => session.CurrentUser),
            new ApiPosSource(new AuthenticatedApiClient(http, session), session));

        Assert.False(model.CanManageTables);
        await model.CreateTableAsync("T1", 2);
        Assert.Empty(model.Options.Tables);
    }

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
        }))
        { BaseAddress = new Uri("https://example.test/api/v1/") };
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

    [Fact]
    public async Task PosApiSource_UsesAuthenticatedHttpForAllOrderChannelsDiscountAndLoyalty()
    {
        var categoryId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var discountId = Guid.NewGuid();
        var calls = new List<(string Method, string Path, string? Bearer, string Body)>();
        var submittedOrders = new List<CreateOrderRequest>();
        var identity = new Identity(new StaffSession(Guid.NewGuid(), "Test cashier", [], ["orders.create", "orders.confirm", "discounts.request", "loyalty.redeem"]));
        var session = new StaffSessionManager(identity, new Store());
        await session.LoginAsync("fixture", "fixture");
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
            calls.Add((request.Method.Method, request.RequestUri.PathAndQuery, request.Headers.Authorization?.Parameter, body));
            object response = path switch
            {
                "/api/v1/menu-items" => new[] { new MenuItemResponse(itemId, categoryId, "Drinks", "Tea", null, true,
                    [new(variantId, "Small", 5m, true)], []) },
                "/api/v1/categories" => new[] { new CategoryResponse(categoryId, "Drinks", 0, true) },
                "/api/v1/pos/tables" => new[] { new PosTableResponse(tableId, "T1", 4, false, null) },
                "/api/v1/orders" when request.Method == HttpMethod.Post => CreateDraft((await request.Content!.ReadFromJsonAsync<CreateOrderRequest>())!),
                var route when route.EndsWith("/confirm", StringComparison.Ordinal) => Confirmed(Guid.Parse(route.Split('/')[4])),
                var route when route.EndsWith("/discounts", StringComparison.Ordinal) =>
                    new DiscountDecisionResponse(discountId, "pending", null, 0m, 5m),
                var route when route.EndsWith("/loyalty/redeem", StringComparison.Ordinal) =>
                    new LoyaltyOperationResponse(Guid.NewGuid(), 5, 1m, false),
                _ => throw new InvalidOperationException($"Unexpected route {path}"),
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) };

            OrderResponse CreateDraft(CreateOrderRequest draft)
            {
                submittedOrders.Add(draft);
                return new(draft.OrderId, "POS-1", draft.Channel!, "draft", draft.Customer?.CustomerId,
                    draft.TableId, draft.TableId is null ? null : "T1", 5m, 0m, 5m, [], []);
            }

            OrderResponse Confirmed(Guid id) => new(id, "POS-1", submittedOrders.Single(order => order.OrderId == id).Channel!,
                "confirmed", null, null, null, 5m, 0m, 5m, [], []);
        }))
        { BaseAddress = new Uri("https://example.test/api/v1/") };
        var source = new ApiPosSource(new AuthenticatedApiClient(http, session), session);

        Assert.Single(await source.MenuAsync(string.Empty));
        Assert.Single(await source.CategoriesAsync());
        Assert.Single((await source.OptionsAsync()).Tables);
        foreach (var channel in new[] { "dine_in", "takeaway", "delivery" })
        {
            var customer = channel == "delivery"
                ? new CustomerOrderSelectionRequest(IsDelivery: true, RecipientName: "Test Recipient", ContactNumber: "5550101", AddressLine: "1 Test St")
                : new CustomerOrderSelectionRequest();
            var order = await source.CreateAsync(EntityId.New(), channel,
                channel == "dine_in" ? new EntityId(tableId) : null, customer,
                [new(new(itemId), new(variantId), 1, [])]);
            var confirmed = await source.ConfirmAsync(order.Id, order.Total);
            Assert.Equal("confirmed", confirmed.Status);
        }
        await source.RequestDiscountAsync(new(submittedOrders[0].OrderId), new(discountId), "fixed", 1m, "Service recovery");
        await source.RedeemAsync(new(submittedOrders[1].OrderId), 5);

        Assert.Equal(new[] { "dine_in", "takeaway", "delivery" }, submittedOrders.Select(order => order.Channel));
        Assert.Equal(tableId, submittedOrders[0].TableId);
        Assert.Null(submittedOrders[1].TableId);
        Assert.Equal("1 Test St", submittedOrders[2].Customer!.AddressLine);
        Assert.Contains(calls, call => call.Method == "POST" && call.Path.EndsWith("/discounts", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Method == "POST" && call.Path.EndsWith("/loyalty/redeem", StringComparison.Ordinal));
        Assert.All(calls, call => Assert.Equal("fixture-access", call.Bearer));
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
