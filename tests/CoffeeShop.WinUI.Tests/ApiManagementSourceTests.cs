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
    public async Task CatalogWorkspaceSource_CompletesCategoryMenuVariantAddonAndRecipeWorkflowsOverHttp()
    {
        var categoryId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var calls = new List<(string Method, string Path, string? Token)>();
        using var client = CreateClient(request =>
        {
            calls.Add((request.Method.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter));
            if (request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath == "/api/v1/categories")
                return Json(new CategoryResponse(categoryId, "Coffee", 0, true));
            if (request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath == "/api/v1/menu-items")
                return Json(new MenuItemResponse(itemId, categoryId, "Coffee", "Latte", null, true, [], []));
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == "/api/v1/categories")
                return Json(Array.Empty<CategoryResponse>());
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == "/api/v1/menu-items")
                return Json(Array.Empty<MenuItemResponse>());
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == "/api/v1/addons")
                return Json(Array.Empty<AddonResponse>());
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == "/api/v1/ingredients")
                return Json(new PageResponse<IngredientBalanceResponse>([], 1, 200, 0));
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var source = new ApiCatalogWorkspaceSource(new AuthenticatedApiClient(client, CreateSession()));

        Assert.True((await source.SearchCategoriesAsync(new())).IsSuccess);
        Assert.True((await source.SearchMenuItemsAsync(new())).IsSuccess);
        Assert.True((await source.ListAddonsAsync()).IsSuccess);
        Assert.True((await source.ListActiveRecipeIngredientsAsync()).IsSuccess);
        Assert.True((await source.CreateCategoryAsync(new("Coffee", 1))).IsSuccess);
        Assert.True((await source.UpdateCategoryAsync(new(new(categoryId), "Hot drinks"))).IsSuccess);
        Assert.True((await source.DeactivateCategoryAsync(new(new(categoryId)))).IsSuccess);
        Assert.True((await source.CreateMenuItemAsync(new(new(categoryId), "Latte", null, [new("Small", 4m)]))).IsSuccess);
        Assert.True((await source.UpdateMenuItemAsync(new(new(itemId), new(categoryId), "Latte", "Milk coffee"))).IsSuccess);
        Assert.True((await source.DeactivateMenuItemAsync(new(new(itemId)))).IsSuccess);
        Assert.True((await source.CreateSizeVariantAsync(new(new(itemId), "Large", 6m))).IsSuccess);
        Assert.True((await source.UpdateSizeVariantAsync(new(new(itemId), new(variantId), "Large", 6m))).IsSuccess);
        Assert.True((await source.DeactivateSizeVariantAsync(new(new(variantId)))).IsSuccess);
        Assert.True((await source.CreateAddonAsync(new("Extra shot", 1m))).IsSuccess);
        Assert.True((await source.UpdateAddonAsync(new(new(addonId), "Double shot", 2m))).IsSuccess);
        Assert.True((await source.DeactivateAddonAsync(new(new(addonId)))).IsSuccess);
        Assert.True((await source.SetMenuItemAddonsAsync(new(new(itemId), [new(addonId)]))).IsSuccess);
        Assert.True((await source.ReplaceRecipeAsync(new(new(variantId), [new(new(ingredientId), 20m, "g")]))).IsSuccess);

        Assert.Contains(calls, call => call.Method == "GET" && call.Path.Contains("ingredients?isActive=true", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Method == "PUT" && call.Path == $"/api/v1/recipes/{variantId}");
        Assert.Contains(calls, call => call.Method == "PUT" && call.Path == $"/api/v1/menu-items/{itemId}/addons");
        Assert.Equal(14, calls.Count(call => call.Method is "POST" or "PUT"));
        Assert.All(calls, call => Assert.Equal("fixture-access", call.Token));
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
    public async Task CustomerWorkspaceSource_ManagesProfileAndSavedDeliveryAddressOverHttp()
    {
        var customerId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var calls = new List<(string Method, string Path, string? Token)>();
        var detail = new CustomerDetailResponse(customerId, "Ada Lovelace", "5550101", "ada@example.test", true, 35,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            [new(addressId, "12 Example St", "Home", true, true)], []);
        using var client = CreateClient(request =>
        {
            calls.Add((request.Method.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter));
            if (request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath == "/api/v1/customers") return Json(detail);
            if (request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath.EndsWith("/addresses", StringComparison.Ordinal))
                return Json(new CustomerAddressResponse(addressId, "12 Example St", "Home", true, true));
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == $"/api/v1/customers/{customerId}") return Json(detail);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var source = new ApiCustomerWorkspaceSource(new AuthenticatedApiClient(client, CreateSession()));

        var created = await source.CreateAsync(new("Ada Lovelace", "5550101", "ada@example.test"));
        Assert.True(created.IsSuccess);
        Assert.Equal(new EntityId(customerId), created.Value);
        Assert.True((await source.UpdateAsync(new(new(customerId), "Ada Lovelace", "5550101", "ada@example.test"))).IsSuccess);
        Assert.True((await source.AddAddressAsync(new(new(customerId), "12 Example St", "Home", true))).IsSuccess);
        Assert.True((await source.UpdateAddressAsync(new(new(customerId), new(addressId), "14 Example St", "Home", false))).IsSuccess);
        Assert.True((await source.SetDefaultAddressAsync(new(new(customerId), new(addressId)))).IsSuccess);
        Assert.True((await source.DeactivateAddressAsync(new(new(customerId), new(addressId)))).IsSuccess);
        var selection = await source.PrepareOrderSelectionAsync(new(new(customerId), IsDelivery: true,
            CustomerAddressId: new(addressId), RecipientName: "Ada", ContactNumber: "5550101"));
        Assert.Equal("12 Example St", selection.Value.AddressLine);
        Assert.True((await source.DeactivateAsync(new(new(customerId)))).IsSuccess);

        Assert.Contains(calls, call => call.Method == "PATCH" && call.Path == $"/api/v1/customers/{customerId}");
        Assert.Contains(calls, call => call.Method == "POST" && call.Path == $"/api/v1/customers/{customerId}/addresses/{addressId}/default");
        Assert.Contains(calls, call => call.Method == "POST" && call.Path == $"/api/v1/customers/{customerId}/deactivate");
        Assert.All(calls, call => Assert.Equal("fixture-access", call.Token));
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

    [Fact]
    public async Task InventorySource_PostsReceiptAndAdjustmentOnceThenReadsAuthoritativeBalanceAndHistory()
    {
        var ingredientId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var receiptLineId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var requests = new List<(string Method, string Path, string? Token, string Body)>();
        var receiptPosted = false;
        var balance = 2m;
        using var client = CreateClient(async request =>
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync();
            requests.Add((request.Method.Method, request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.Parameter, body));
            var receipt = new ReceiptResponse(receiptId, supplierId, "RCPT-200", receiptPosted ? "posted" : "draft",
                receiptPosted ? DateTimeOffset.UtcNow : null,
                [new(receiptLineId, ingredientId, 4m, 3m)], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            return request.RequestUri.AbsolutePath switch
            {
                "/api/v1/ingredients" when request.Method == HttpMethod.Get => Json(new PageResponse<IngredientBalanceResponse>(
                    [new(ingredientId, "Whole milk", "ml", balance, 5m, true)], 1, 200, 1)),
                "/api/v1/purchase-receipts" when request.Method == HttpMethod.Post => Json(new ReceiptResponse(
                    receiptId, supplierId, "RCPT-200", "draft", null, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)),
                "/api/v1/purchase-receipts" when request.Method == HttpMethod.Get => Json(new[] { receipt }),
                var path when path == $"/api/v1/purchase-receipts/{receiptId}/lines" => Json(receipt),
                var path when path == $"/api/v1/purchase-receipts/{receiptId}/post" => MarkReceiptPosted(),
                var path when path == $"/api/v1/ingredients/{ingredientId}/adjustments" => Json(RecordAdjustment()),
                "/api/v1/stock-movements" => Json(new[]
                {
                    new StockMovementResponse(Guid.NewGuid(), ingredientId, "adjustment", 1m, actorId,
                        "Cycle count correction", DateTimeOffset.UtcNow, null, null),
                    new StockMovementResponse(Guid.NewGuid(), ingredientId, "receipt", 4m, actorId,
                        "Receipt RCPT-200", DateTimeOffset.UtcNow, receiptId, null),
                }),
                var path when path == $"/api/v1/ingredients/{ingredientId}/movements" => Json(new[]
                {
                    new StockMovementResponse(Guid.NewGuid(), ingredientId, "adjustment", 1m, actorId,
                        "Cycle count correction", DateTimeOffset.UtcNow, null, null),
                    new StockMovementResponse(Guid.NewGuid(), ingredientId, "receipt", 4m, actorId,
                        "Receipt RCPT-200", DateTimeOffset.UtcNow, receiptId, null),
                }),
                _ => throw new InvalidOperationException($"Unexpected route {request.RequestUri.AbsolutePath}"),
            };

            HttpResponseMessage MarkReceiptPosted()
            {
                receiptPosted = true;
                balance += 4m;
                return new(HttpStatusCode.NoContent);
            }

            StockAdjustmentResponse RecordAdjustment()
            {
                balance += 1m;
                return new(ingredientId, balance);
            }
        });
        var source = new ApiInventorySource(new AuthenticatedApiClient(client, CreateSession()));

        var lowStockBefore = await source.ListIngredientsAsync(includeInactive: false);
        var draft = await source.CreateReceiptAsync(new(supplierId), "RCPT-200");
        await source.AddReceiptLineAsync(draft.Id, new(ingredientId), 4m, 3m);
        await source.PostReceiptAsync(draft.Id);
        var postedReceipt = Assert.Single(await source.ListReceiptsAsync());
        await source.AdjustAsync(new(ingredientId), 1m, "Cycle count correction");
        var authoritativeBalance = Assert.Single(await source.ListIngredientsAsync());
        var movements = await source.ListMovementsAsync(new(ingredientId));

        var lowStockIngredient = Assert.Single(lowStockBefore);
        Assert.Equal(2m, lowStockIngredient.AvailableQuantity);
        Assert.True(lowStockIngredient.AvailableQuantity <= lowStockIngredient.ReorderThreshold);
        Assert.Equal(PurchaseReceiptStatus.Posted, postedReceipt.Status);
        Assert.Equal(4m, Assert.Single(postedReceipt.Lines).Quantity);
        Assert.Equal(7m, authoritativeBalance.AvailableQuantity);
        Assert.True(authoritativeBalance.AvailableQuantity > authoritativeBalance.ReorderThreshold);
        Assert.Equal(2, movements.Count);
        Assert.Contains(movements, movement => movement.Type == StockMovementType.Receipt
            && movement.PurchaseReceiptId == new EntityId(receiptId));
        Assert.Contains(movements, movement => movement.Type == StockMovementType.Adjustment
            && movement.Reason == "Cycle count correction" && movement.ActorId == new EntityId(actorId));
        Assert.Contains("\"reason\":\"Cycle count correction\"", Assert.Single(requests,
            request => request.Path.EndsWith("/adjustments", StringComparison.Ordinal)).Body);
        Assert.Equal(1, requests.Count(request => request.Method == "POST" && request.Path.EndsWith("/post", StringComparison.Ordinal)));
        Assert.Contains(requests, request => request.Path == "/api/v1/ingredients?page=1&pageSize=200&isActive=true");
        Assert.All(requests, request => Assert.Equal("fixture-access", request.Token));
    }

    [Fact]
    public async Task InventorySource_ManagesSupplierAndIngredientLifecycleThroughAuthenticatedHttp()
    {
        var supplierId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var calls = new List<(string Method, string Path, string? Token)>();
        using var client = CreateClient(request =>
        {
            calls.Add((request.Method.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter));
            if (request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath == "/api/v1/suppliers")
                return Json(new SupplierResponse(supplierId, "North Farm", "5550199", null, "North district", true));
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == "/api/v1/suppliers")
                return Json(new[] { new SupplierResponse(supplierId, "North Farm", "5550199", null, "North district", true) });
            if (request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath == "/api/v1/ingredients")
                return Json(new IngredientBalanceResponse(ingredientId, "Coffee beans", "g", 100m, 20m, true));
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == "/api/v1/ingredients")
                return Json(new PageResponse<IngredientBalanceResponse>(
                    [new(ingredientId, "Coffee beans", "g", 100m, 25m, true)], 1, 200, 1));
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var source = new ApiInventorySource(new AuthenticatedApiClient(client, CreateSession()));

        var supplier = await source.CreateSupplierAsync("North Farm", "5550199", null, "North district");
        Assert.Equal(supplierId, supplier.Id.Value);
        Assert.Equal(supplierId, (await source.FindSupplierAsync(new(supplierId)))!.Id.Value);
        await source.UpdateSupplierAsync(new(supplierId), "North Farm Updated", "5550199", null, "North district");
        await source.DeactivateSupplierAsync(new(supplierId));
        await source.ActivateSupplierAsync(new(supplierId));
        var ingredient = await source.CreateIngredientAsync("Coffee beans", "g", 100m, 20m);
        Assert.Equal(ingredientId, ingredient.Id.Value);
        await source.SetThresholdAsync(new(ingredientId), 25m);
        await source.DeactivateIngredientAsync(new(ingredientId));
        await source.ActivateIngredientAsync(new(ingredientId));
        await source.ListIngredientsAsync(includeInactive: false);

        Assert.Contains(calls, call => call.Method == "PUT" && call.Path == $"/api/v1/suppliers/{supplierId}");
        Assert.Contains(calls, call => call.Method == "POST" && call.Path == $"/api/v1/ingredients/{ingredientId}/threshold");
        Assert.Contains(calls, call => call.Method == "POST" && call.Path == $"/api/v1/suppliers/{supplierId}/deactivate");
        Assert.Contains(calls, call => call.Method == "POST" && call.Path == $"/api/v1/ingredients/{ingredientId}/activate");
        Assert.Contains(calls, call => call.Method == "GET" && call.Path == "/api/v1/ingredients?page=1&pageSize=200&isActive=true");
        Assert.All(calls, call => Assert.Equal("fixture-access", call.Token));
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
