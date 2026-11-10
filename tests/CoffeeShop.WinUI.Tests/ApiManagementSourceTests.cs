using System.Net;
using System.Net.Http.Json;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.WinUI.Services;

namespace CoffeeShop.WinUI.Tests;

public sealed class ApiManagementSourceTests
{
    [Fact]
    public async Task CatalogWorkspaceSource_MapsSearchAndSendsCategoryAndRecipeWritesOverAuthenticatedHttp()
    {
        var categoryId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var requests = new List<(string Method, string Path, string? Token)>();
        using var client = CreateClient(request =>
        {
            requests.Add((request.Method.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter));
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.EndsWith("/categories"))
                return Json(new[] { new CategoryResponse(categoryId, "Coffee", 1, true) });
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.EndsWith("/menu-items"))
                return Json(new[] { new MenuItemResponse(itemId, categoryId, "Coffee", "Latte", "Milk", true,
                    [new SizeVariantResponse(variantId, "Small", 4m, true)], []) });
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.EndsWith($"/menu-items/{itemId}"))
                return Json(new MenuItemResponse(itemId, categoryId, "Coffee", "Latte", "Milk", true,
                    [new SizeVariantResponse(variantId, "Small", 4m, true,
                        [new RecipeLineResponse(Guid.NewGuid(), ingredientId, "Milk", 120m, "ml")])], []));
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var source = new ApiCatalogWorkspaceSource(new AuthenticatedApiClient(client, CreateSession()));

        var categories = await source.SearchCategoriesAsync(new(Limit: 200));
        var items = await source.SearchMenuItemsAsync(new("latte", IsActive: true, Limit: 200));
        var detail = await source.GetMenuItemDetailAsync(new(itemId));
        var categoryUpdate = await source.UpdateCategoryAsync(new(new(categoryId), "Hot drinks"));
        var recipeUpdate = await source.ReplaceRecipeAsync(new(new(variantId), [new(new(ingredientId), 120m, "ml")]));

        Assert.True(categories.IsSuccess);
        Assert.Equal("Coffee", Assert.Single(categories.Value).Name);
        Assert.Equal("Latte", Assert.Single(items.Value).Name);
        Assert.Equal(120m, Assert.Single(Assert.Single(detail.Value.SizeVariants).Recipe).Quantity);
        Assert.True(categoryUpdate.IsSuccess);
        Assert.True(recipeUpdate.IsSuccess);
        Assert.Contains(requests, request => request.Method == "GET" && request.Path == "/api/v1/categories?search=&limit=200");
        Assert.Contains(requests, request => request.Method == "GET" && request.Path == "/api/v1/menu-items?search=latte&limit=200&isActive=true");
        Assert.Contains(requests, request => request.Method == "PUT" && request.Path == $"/api/v1/categories/{categoryId}");
        Assert.Contains(requests, request => request.Method == "PUT" && request.Path == $"/api/v1/recipes/{variantId}");
        Assert.All(requests, request => Assert.Equal("fixture-access", request.Token));
    }

    [Fact]
    public async Task CustomerWorkspaceSource_UsesCustomerAndAddressHttpRoutesAndMapsLoyaltyHistory()
    {
        var customerId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var requests = new List<(string Method, string Path, string? Token)>();
        using var client = CreateClient(request =>
        {
            requests.Add((request.Method.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter));
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.EndsWith("/customers"))
                return Json(new[] { new CustomerResponse(customerId, "Ada Lovelace", "5550101", null, true, 35) });
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.EndsWith("/loyalty-transactions"))
                return Json(new LoyaltyHistoryResponse([new(Guid.NewGuid(), "earned", 5, 35, null, null, DateTimeOffset.UtcNow, null)], 0, 20, 1));
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var source = new ApiCustomerWorkspaceSource(new AuthenticatedApiClient(client, CreateSession()));

        var customers = await source.SearchAsync(new("Ada", false, 100));
        var allCustomers = await source.SearchAsync(new("Ada", true, 100));
        var history = await source.GetHistoryAsync(new(customerId), new(0, 20));
        var update = await source.UpdateAddressAsync(new(new(customerId), new(addressId), "12 Example St", "Home", true));

        Assert.Equal("Ada Lovelace", Assert.Single(customers.Value).FullName);
        Assert.True(allCustomers.IsSuccess);
        Assert.Equal(5, Assert.Single(history.Value.Items).Points);
        Assert.True(update.IsSuccess);
        Assert.Contains(requests, request => request.Method == "GET" && request.Path == "/api/v1/customers?search=Ada&limit=100&isActive=true");
        Assert.Contains(requests, request => request.Method == "GET" && request.Path == "/api/v1/customers?search=Ada&limit=100&includeInactive=true");
        Assert.Contains(requests, request => request.Method == "PATCH" && request.Path == $"/api/v1/customers/{customerId}/addresses/{addressId}");
        Assert.All(requests, request => Assert.Equal("fixture-access", request.Token));
    }

    [Fact]
    public async Task InventorySource_UsesServerActorForStockAdjustmentsAndMapsReceiptAndMovementData()
    {
        var ingredientId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var receiptLineId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddHours(-1);
        var postedAt = DateTimeOffset.UtcNow;
        var requestBodies = new List<string>();
        using var client = CreateClient(async request =>
        {
            if (request.Content is not null) requestBodies.Add(await request.Content.ReadAsStringAsync());
            return request.RequestUri!.AbsolutePath switch
            {
                "/api/v1/ingredients" => Json(new PageResponse<IngredientBalanceResponse>(
                    [new(ingredientId, "Milk", "ml", 900m, 200m, true)], 1, 200, 1)),
                "/api/v1/purchase-receipts" => Json(new[] { new ReceiptResponse(receiptId, supplierId,
                    "RCPT-API-153", "posted", postedAt,
                    [new ReceiptLineResponse(receiptLineId, ingredientId, 3m, 2m)], createdAt, postedAt) }),
                var path when path.EndsWith("/adjustments") => Json(new StockAdjustmentResponse(ingredientId, 950m)),
                "/api/v1/stock-movements" => Json(new[] { new StockMovementResponse(Guid.NewGuid(), ingredientId,
                    "adjustment", 50m, actorId, "Counted stock", DateTimeOffset.UtcNow, null, null) }),
                _ => throw new InvalidOperationException($"Unexpected route {request.RequestUri.AbsolutePath}"),
            };
        });
        var source = new ApiInventorySource(new AuthenticatedApiClient(client, CreateSession()));

        var ingredients = await source.ListIngredientsAsync();
        var receipts = await source.ListReceiptsAsync();
        var adjustment = await source.AdjustAsync(new(ingredientId), 50m, "Counted stock");
        var movements = await source.ListMovementsAsync();

        Assert.Equal(900m, Assert.Single(ingredients).AvailableQuantity);
        Assert.True(Assert.Single(ingredients).IsActive);
        var receipt = Assert.Single(receipts);
        Assert.Equal(PurchaseReceiptStatus.Posted, receipt.Status);
        Assert.Equal(createdAt, receipt.CreatedAt.Value);
        Assert.Equal(postedAt, receipt.PostedAt!.Value);
        Assert.Equal(3m, Assert.Single(receipt.Lines).Quantity);
        Assert.Equal(new Money(2m), Assert.Single(receipt.Lines).UnitCost);
        Assert.Equal(950m, adjustment.AvailableQuantity);
        Assert.Equal(actorId, Assert.Single(movements).ActorId.Value);
        Assert.DoesNotContain("actorId", string.Join("", requestBodies), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requestId", string.Join("", requestBodies), StringComparison.OrdinalIgnoreCase);
    }

    private static StaffSessionManager CreateSession()
    {
        var manager = new StaffSessionManager(new Identity(), new Store());
        manager.LoginAsync("fixture", "fixture").GetAwaiter().GetResult();
        return manager;
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new Handler(request => Task.FromResult(respond(request)))) { BaseAddress = new Uri("https://example.test/api/v1/") };
    private static HttpClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) =>
        new(new Handler(respond)) { BaseAddress = new Uri("https://example.test/api/v1/") };
    private static HttpResponseMessage Json<T>(T payload) => new(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
    private sealed class Identity : IStaffIdentityApi
    {
        public Task<TokenResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TokenResponse("fixture-access", "fixture-refresh", DateTimeOffset.UtcNow.AddHours(1),
                new StaffSession(Guid.NewGuid(), "Fixture", [], ["menu.manage", "customers.manage", "inventory.manage", "inventory.adjust"])));
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
