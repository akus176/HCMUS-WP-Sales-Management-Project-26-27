namespace CoffeeShop.Api.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CoffeeShop.Api;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

public sealed class StaffIdentityHttpTests : IAsyncLifetime
{
    private readonly string databaseName = $"coffeeshop_identity_{Guid.NewGuid():N}";
    private const string Password = "test-only-valid-staff-password-150";
    private string? adminConnection;
    private string? connectionString;
    private WebApplicationFactory<Program>? host;

    public async Task InitializeAsync()
    {
        adminConnection = Environment.GetEnvironmentVariable("COFFEESHOP_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        await using (var connection = new NpgsqlConnection(adminConnection))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
        connectionString = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName, Pooling = false }.ConnectionString;
        await using var db = Database();
        await db.Database.MigrateAsync();
        await StaffRoleSeed.ApplyAsync(db);
        await StaffRoleSeed.ApplyAsync(db);
        foreach (var role in StaffRoleSeed.Bundles.Keys)
            await AddUserAsync(db, role, [role]);
        await AddUserAsync(db, "multi", ["Cashier", "Barista"]);
        await AddUserAsync(db, "inactive", ["Cashier"], false);
        host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseTestIdentity().UseSetting("ConnectionStrings:CoffeeShop", connectionString));
    }

    private CoffeeShopDbContext Database()
    {
        var options = new DbContextOptionsBuilder<CoffeeShopDbContext>();
        CoffeeShopPostgresOptions.Configure(options, connectionString!);
        return new(options.Options);
    }

    private static async Task AddUserAsync(CoffeeShopDbContext db, string name, string[] roles, bool active = true)
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var user = new User(EntityId.New(), name, $"{name}@example.test", "temporary", name, null, now, now, active);
        db.Entry(user).Property(u => u.PasswordHash).CurrentValue = StaffIdentityService.HashPassword(user, Password);
        db.Users.Add(user);
        foreach (var role in roles)
        {
            var stored = await db.Roles.SingleAsync(r => r.Code == role);
            db.UserRoles.Add(new(user.Id, stored.Id, now));
        }
        await db.SaveChangesAsync();
    }

    private async Task<TokenResponse> LoginAsync(HttpClient client, string username = "Cashier")
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    [PostgresFact]
    public async Task LocalProvisioning_PersistsHashedPasswordAndExplicitRoleAssignments()
    {
        var values = new Dictionary<string, string?>
        {
            ["ProvisionStaff:Username"] = "provisioned",
            ["ProvisionStaff:Email"] = "provisioned@example.test",
            ["ProvisionStaff:FullName"] = "Provisioned Staff",
            ["ProvisionStaff:Password"] = Password,
            ["ProvisionStaff:Roles"] = "Cashier,Barista",
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        await StaffProvisioning.RunAsync(host!.Services, configuration, true);
        using var client = host.CreateClient();
        var pair = await LoginAsync(client, "provisioned");
        Assert.Equal(new[] { "Barista", "Cashier" }, pair.User.Roles);
        await using var db = Database();
        var user = await db.Users.SingleAsync(u => u.Username == "provisioned");
        Assert.NotEqual(Password, user.PasswordHash);
        await Assert.ThrowsAsync<InvalidOperationException>(() => StaffProvisioning.RunAsync(host.Services, configuration, true));
    }

    [PostgresFact]
    public async Task SeedAndLogin_CoverSixRolesAndMultiRolePermissions()
    {
        using var client = host!.CreateClient();
        await using var db = Database();
        Assert.Equal(6, await db.Roles.CountAsync());
        foreach (var (role, permissions) in StaffRoleSeed.Bundles)
        {
            var pair = await LoginAsync(client, role);
            Assert.Equal([role], pair.User.Roles);
            Assert.Equal(permissions.Order(), pair.User.Permissions);
            client.DefaultRequestHeaders.Authorization = new("Bearer", pair.AccessToken);
            var me = await client.GetFromJsonAsync<StaffSession>("/api/v1/auth/me");
            Assert.Equal(pair.User.Permissions, me!.Permissions);
            using var write = await client.PostAsJsonAsync("/api/v1/categories", new { name = $"Role {Guid.NewGuid():N}" });
            Assert.Equal(permissions.Contains("menu.manage") ? HttpStatusCode.Created : HttpStatusCode.Forbidden, write.StatusCode);
            using var scope = host.Services.CreateScope();
            var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(pair.User.Permissions.Select(p => new Claim("permission", p)), "Bearer"));
            foreach (var policy in StaffRoleSeed.Bundles.Values.SelectMany(p => p).Distinct())
                Assert.Equal(permissions.Contains(policy), (await authorization.AuthorizeAsync(principal, null, policy)).Succeeded);
        }
        var multi = await LoginAsync(client, "multi");
        Assert.Equal(new[] { "Barista", "Cashier" }, multi.User.Roles);
        Assert.Contains("orders.fulfill", multi.User.Permissions);
        Assert.Contains("payments.record", multi.User.Permissions);
        Assert.DoesNotContain("users.manage", multi.User.Permissions);
    }

    [PostgresFact]
    public async Task LoginFailures_AreGenericAndAuditedWithoutCredentials()
    {
        using var client = host!.CreateClient();
        string? expected = null;
        foreach (var request in new[] { new LoginRequest("Cashier", "wrong"), new("unknown", Password), new("inactive", Password) })
        {
            using var response = await client.PostAsJsonAsync("/api/v1/auth/login", request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            // ProblemDetails may add a request trace identifier; stable public fields stay generic.
            var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
            expected ??= problem!.Detail;
            Assert.Equal(expected, problem!.Detail);
            Assert.DoesNotContain(Password, body);
            Assert.DoesNotContain("accessToken", body);
        }
        await using var db = Database();
        var audits = await db.SecurityAuditEvents.Where(e => e.EventType == "login_failed").ToArrayAsync();
        Assert.Equal(3, audits.Length);
        Assert.All(audits, audit => Assert.Null(audit.Metadata));
    }

    [PostgresFact]
    public async Task Tokens_HaveMinimalClaimsExpiryAndOnlyHashesInDatabase()
    {
        using var client = host!.CreateClient();
        var pair = await LoginAsync(client);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(pair.AccessToken);
        Assert.Equal("coffeeshop-test", jwt.Issuer);
        Assert.Equal("staff-test", Assert.Single(jwt.Audiences));
        Assert.InRange((jwt.ValidTo - jwt.ValidFrom).TotalMinutes, 9.9, 10.1);
        Assert.Equal(pair.User.Id.ToString(), jwt.Subject);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type is "email" or "password" or "password_hash" or "refreshToken");
        await using var db = Database();
        var stored = await db.RefreshTokens.SingleAsync();
        Assert.Equal(StaffIdentityService.HashToken(pair.RefreshToken), stored.TokenHash);
        Assert.NotEqual(pair.RefreshToken, stored.TokenHash);
        Assert.DoesNotContain(Password, pair.AccessToken);
        var tampered = pair.AccessToken[..^8] + "AAAAAAAA";
        client.DefaultRequestHeaders.Authorization = new("Bearer", tampered);
        using var denied = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [PostgresFact]
    public async Task InvalidAndExpiredTokens_AreRejected()
    {
        using var client = host!.CreateClient();
        var pair = await LoginAsync(client);
        foreach (var (issuer, audience, expired) in new[] { ("wrong", "staff-test", false), ("coffeeshop-test", "wrong", false), ("coffeeshop-test", "staff-test", true) })
        {
            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(issuer, audience, [new Claim("sub", pair.User.Id.ToString())],
                now.AddMinutes(-20), expired ? now.AddMinutes(-10) : now.AddMinutes(10),
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestIdentitySettings.SigningKey)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            using var denied = await client.GetAsync("/api/v1/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        await using (var db = Database())
        {
            var stored = await db.RefreshTokens.SingleAsync();
            db.Entry(stored).Property(t => t.ExpiresAt).CurrentValue = new UtcTimestamp(DateTimeOffset.UtcNow.AddMinutes(-1));
            await db.SaveChangesAsync();
        }
        foreach (var token in new[] { pair.RefreshToken, "unknown-token", "" })
        {
            using var denied = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(token));
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
    }

    [PostgresFact]
    public async Task RefreshRotation_ReuseRevokesFamilyButNotIndependentSession()
    {
        using var client = host!.CreateClient();
        var original = await LoginAsync(client);
        var independent = await LoginAsync(client);
        using var rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(original.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var pair = (await rotated.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(original.RefreshToken, pair.RefreshToken);
        using var reused = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(original.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        using var familyDenied = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(pair.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, familyDenied.StatusCode);
        using var independentOk = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(independent.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, independentOk.StatusCode);
        await using var db = Database();
        Assert.True(await db.SecurityAuditEvents.AnyAsync(e => e.EventType == "token_reuse_detected"));
    }

    [PostgresFact]
    public async Task ConcurrentRefresh_OnlyOneSucceedsAndReuseInvalidatesSuccessor()
    {
        using var client = host!.CreateClient();
        var original = await LoginAsync(client);
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(original.RefreshToken))));
        try
        {
            var success = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);
            var pair = (await success.Content.ReadFromJsonAsync<TokenResponse>())!;
            using var denied = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(pair.RefreshToken));
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [PostgresFact]
    public async Task Logout_EnforcesOwnershipAndAccountWideRevocation()
    {
        using var client = host!.CreateClient();
        var first = await LoginAsync(client);
        var second = await LoginAsync(client);
        var other = await LoginAsync(client, "Manager");
        client.DefaultRequestHeaders.Authorization = new("Bearer", first.AccessToken);
        using var wrongOwner = await client.PostAsJsonAsync("/api/v1/auth/logout", new RefreshRequest(other.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongOwner.StatusCode);
        using var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new RefreshRequest(first.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var denied = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(first.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var all = await client.PostAsync("/api/v1/auth/logout-all", null);
        Assert.Equal(HttpStatusCode.NoContent, all.StatusCode);
        using var secondDenied = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(second.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, secondDenied.StatusCode);
        using var otherOk = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(other.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, otherOk.StatusCode);
    }

    [PostgresFact]
    public async Task AccountAndRoleChanges_AreAuditedAndTakeEffectOnNextRequest()
    {
        using var client = host!.CreateClient();
        var pair = await LoginAsync(client, "Manager");
        client.DefaultRequestHeaders.Authorization = new("Bearer", pair.AccessToken);
        await using (var db = Database())
        {
            var id = new EntityId(pair.User.Id);
            db.UserRoles.RemoveRange(await db.UserRoles.Where(r => r.UserId == id).ToArrayAsync());
            await db.SaveChangesAsync();
        }
        using var forbidden = await client.PostAsJsonAsync("/api/v1/categories", new { name = "Denied after role removal" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("application/problem+json", forbidden.Content.Headers.ContentType?.MediaType);
        await using (var db = Database())
        {
            var user = await db.Users.SingleAsync(u => u.Id == new EntityId(pair.User.Id));
            db.Entry(user).Property(u => u.IsActive).CurrentValue = false;
            await db.SaveChangesAsync();
        }
        using var denied = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal("application/problem+json", denied.Content.Headers.ContentType?.MediaType);
        await using (var db = Database())
        {
            var user = await db.Users.SingleAsync(u => u.Id == new EntityId(pair.User.Id));
            db.Entry(user).Property(u => u.IsActive).CurrentValue = true;
            await db.SaveChangesAsync();
        }
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(pair.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        await using var check = Database();
        Assert.True(await check.SecurityAuditEvents.AnyAsync(e => e.EventType == "account_deactivated"));
        Assert.True(await check.SecurityAuditEvents.AnyAsync(e => e.EventType == "account_reactivated"));
        Assert.True(await check.SecurityAuditEvents.AnyAsync(e => e.EventType == "role_changed"));
        var metadata = string.Join("", await check.SecurityAuditEvents.Select(e => e.Metadata).ToArrayAsync());
        Assert.DoesNotContain(Password, metadata);
        Assert.DoesNotContain(pair.AccessToken, metadata);
        Assert.DoesNotContain(pair.RefreshToken, metadata);
    }

    [PostgresFact]
    public async Task PasswordChange_RevokesAllRefreshFamilies()
    {
        using var client = host!.CreateClient();
        var first = await LoginAsync(client);
        var second = await LoginAsync(client);
        const string newPassword = "test-only-replacement-password-150";
        await using (var db = Database())
        {
            var user = await db.Users.SingleAsync(u => u.Id == new EntityId(first.User.Id));
            db.Entry(user).Property(u => u.PasswordHash).CurrentValue = StaffIdentityService.HashPassword(user, newPassword);
            await db.SaveChangesAsync();
        }
        foreach (var token in new[] { first.RefreshToken, second.RefreshToken })
        {
            using var denied = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(token));
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        using var previousPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("Cashier", Password));
        Assert.Equal(HttpStatusCode.Unauthorized, previousPassword.StatusCode);
        using var changedPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("Cashier", newPassword));
        Assert.Equal(HttpStatusCode.OK, changedPassword.StatusCode);
    }

    [PostgresFact]
    public async Task SalesWorkflow_PersistsDraftPaymentAndPreparationAcrossRoles()
    {
        using var manager = host!.CreateClient();
        var managerLogin = await LoginAsync(manager, "Manager");
        manager.DefaultRequestHeaders.Authorization = new("Bearer", managerLogin.AccessToken);
        using var categoryCreated = await manager.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("Sales drinks"));
        var category = (await categoryCreated.Content.ReadFromJsonAsync<CategoryResponse>())!;
        using var created = await manager.PostAsJsonAsync("/api/v1/menu-items",
            new MenuItemRequest(category.Id, "Latte", null, [new SizeVariantRequest("Regular", 5m)]));
        var item = (await created.Content.ReadFromJsonAsync<MenuItemResponse>())!;
        var variant = Assert.Single(item.SizeVariants);

        using var cashier = host.CreateClient();
        var cashierLogin = await LoginAsync(cashier, "Cashier");
        cashier.DefaultRequestHeaders.Authorization = new("Bearer", cashierLogin.AccessToken);
        var orderId = Guid.NewGuid();
        var draftRequest = new CreateOrderRequest(orderId, "takeaway", null, null,
            [new OrderLineRequest(item.Id, variant.Id, 1, [])]);
        using var createdDraft = await cashier.PostAsJsonAsync("/api/v1/orders", draftRequest);
        Assert.Equal(HttpStatusCode.Created, createdDraft.StatusCode);
        var draft = (await createdDraft.Content.ReadFromJsonAsync<OrderResponse>())!;
        Assert.Equal(5m, draft.Total);
        using var retriedDraft = await cashier.PostAsJsonAsync("/api/v1/orders", draftRequest);
        Assert.Equal(HttpStatusCode.Created, retriedDraft.StatusCode);
        await using (var db = Database()) Assert.Single(await db.Orders.Where(order => order.Id == new EntityId(orderId)).ToArrayAsync());
        using var wrongTotal = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/confirm", new ConfirmOrderRequest(4m));
        Assert.Equal(HttpStatusCode.Conflict, wrongTotal.StatusCode);
        using var confirmed = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/confirm", new ConfirmOrderRequest(5m));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        var paymentId = Guid.NewGuid();
        var payment = new RecordPaymentRequest(paymentId, "cash", 5m);
        using var paid = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", payment);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        using var repeated = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", payment);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        using var mismatch = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", payment with { Amount = 4m });
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);

        using var barista = host.CreateClient();
        var baristaLogin = await LoginAsync(barista, "Barista");
        barista.DefaultRequestHeaders.Authorization = new("Bearer", baristaLogin.AccessToken);
        var tickets = await barista.GetFromJsonAsync<PreparationTicketResponse[]>("/api/v1/preparation-tickets");
        Assert.Contains(tickets!, ticket => ticket.OrderId == orderId && ticket.Lines.Count == 1);
        using var preparing = await barista.PostAsync($"/api/v1/orders/{orderId}/preparing", null);
        Assert.Equal(HttpStatusCode.NoContent, preparing.StatusCode);
        using var ready = await barista.PostAsync($"/api/v1/orders/{orderId}/ready", null);
        Assert.Equal(HttpStatusCode.NoContent, ready.StatusCode);
        using var repeatReady = await barista.PostAsync($"/api/v1/orders/{orderId}/ready", null);
        Assert.Equal(HttpStatusCode.Conflict, repeatReady.StatusCode);
        var shared = await manager.GetFromJsonAsync<OrderResponse>($"/api/v1/orders/{orderId}");
        Assert.Equal("ready", shared!.Status);
    }

    [PostgresFact]
    public async Task CatalogRecipe_IsSharedAcrossSessionsAndRejectsInvalidChanges()
    {
        using var manager = host!.CreateClient();
        var login = await LoginAsync(manager, "Manager");
        manager.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        using var categoryCreated = await manager.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("Shared drinks"));
        var category = (await categoryCreated.Content.ReadFromJsonAsync<CategoryResponse>())!;
        using var ingredientCreated = await manager.PostAsJsonAsync("/api/v1/ingredients",
            new CreateIngredientRequest("Coffee beans", "g", 200m, 10m));
        Assert.Equal(HttpStatusCode.Created, ingredientCreated.StatusCode);
        var ingredient = (await ingredientCreated.Content.ReadFromJsonAsync<IngredientBalanceResponse>())!;
        using var created = await manager.PostAsJsonAsync("/api/v1/menu-items",
            new MenuItemRequest(category.Id, "Espresso", null, [new SizeVariantRequest("Small", 3m)]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = (await created.Content.ReadFromJsonAsync<MenuItemResponse>())!;
        var variant = Assert.Single(item.SizeVariants);
        using var badRecipe = await manager.PutAsJsonAsync($"/api/v1/recipes/{variant.Id}",
            new ReplaceRecipeRequest([new RecipeLineRequest(Guid.Empty, 10m, "g")]));
        Assert.Equal(HttpStatusCode.BadRequest, badRecipe.StatusCode);
        using var recipe = await manager.PutAsJsonAsync($"/api/v1/recipes/{variant.Id}",
            new ReplaceRecipeRequest([new RecipeLineRequest(ingredient.IngredientId, 10m, "g")]));
        Assert.Equal(HttpStatusCode.NoContent, recipe.StatusCode);

        using var barista = host.CreateClient();
        var baristaLogin = await LoginAsync(barista, "Barista");
        barista.DefaultRequestHeaders.Authorization = new("Bearer", baristaLogin.AccessToken);
        var shared = await barista.GetFromJsonAsync<MenuItemResponse>($"/api/v1/menu-items/{item.Id}");
        Assert.Equal(ingredient.IngredientId, Assert.Single(Assert.Single(shared!.SizeVariants).Recipe!).IngredientId);
        using var forbidden = await barista.PostAsJsonAsync("/api/v1/addons", new AddonRequest("Cream", 1m));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [PostgresFact]
    public async Task InventoryAdjustment_EnforcesPermissionValidationAndIdempotency()
    {
        using var client = host!.CreateClient();
        var inventory = await LoginAsync(client, "InventoryStaff");
        client.DefaultRequestHeaders.Authorization = new("Bearer", inventory.AccessToken);
        using var created = await client.PostAsJsonAsync("/api/v1/ingredients",
            new CreateIngredientRequest("Test milk", "l", 5m, 2m));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ingredient = (await created.Content.ReadFromJsonAsync<IngredientBalanceResponse>())!;

        var requestId = Guid.NewGuid();
        var adjustment = new StockAdjustmentRequest(requestId, -3m, "Damaged stock");
        using var first = await client.PostAsJsonAsync($"/api/v1/ingredients/{ingredient.IngredientId}/adjustments", adjustment);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(2m, (await first.Content.ReadFromJsonAsync<StockAdjustmentResponse>())!.AvailableQuantity);
        using var retry = await client.PostAsJsonAsync($"/api/v1/ingredients/{ingredient.IngredientId}/adjustments", adjustment);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var changedRetry = await client.PostAsJsonAsync($"/api/v1/ingredients/{ingredient.IngredientId}/adjustments",
            adjustment with { QuantityDelta = 1m });
        Assert.Equal(HttpStatusCode.Conflict, changedRetry.StatusCode);
        using var missingReason = await client.PostAsJsonAsync($"/api/v1/ingredients/{ingredient.IngredientId}/adjustments",
            new StockAdjustmentRequest(Guid.NewGuid(), 1m, " "));
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);

        var lowStock = await client.GetFromJsonAsync<IngredientBalanceResponse[]>("/api/v1/ingredients/low-stock");
        Assert.Contains(lowStock!, balance => balance.IngredientId == ingredient.IngredientId);
        await using (var db = Database())
            Assert.Single(await db.StockMovements.Where(movement => movement.IngredientId == new EntityId(ingredient.IngredientId)).ToArrayAsync());

        var cashier = await LoginAsync(client, "Cashier");
        client.DefaultRequestHeaders.Authorization = new("Bearer", cashier.AccessToken);
        using var forbidden = await client.PostAsJsonAsync($"/api/v1/ingredients/{ingredient.IngredientId}/adjustments",
            new StockAdjustmentRequest(Guid.NewGuid(), 1m, "Unauthorized"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [PostgresFact]
    public async Task ReceiptPosting_ChangesSharedBalanceAndAddsOneAuditedMovement()
    {
        using var client = host!.CreateClient();
        var inventory = await LoginAsync(client, "InventoryStaff");
        client.DefaultRequestHeaders.Authorization = new("Bearer", inventory.AccessToken);
        using var createdIngredient = await client.PostAsJsonAsync("/api/v1/ingredients",
            new CreateIngredientRequest("Receipt milk", "l", 0m, 1m));
        Assert.Equal(HttpStatusCode.Created, createdIngredient.StatusCode);
        var ingredient = (await createdIngredient.Content.ReadFromJsonAsync<IngredientBalanceResponse>())!;
        using var createdSupplier = await client.PostAsJsonAsync("/api/v1/suppliers",
            new SupplierRequest("Local farm", "555-0100", null, null));
        Assert.Equal(HttpStatusCode.Created, createdSupplier.StatusCode);
        var supplier = (await createdSupplier.Content.ReadFromJsonAsync<SupplierResponse>())!;
        using var createdReceipt = await client.PostAsJsonAsync("/api/v1/purchase-receipts",
            new CreateReceiptRequest(supplier.Id, "RCPT-151"));
        Assert.Equal(HttpStatusCode.Created, createdReceipt.StatusCode);
        var receipt = (await createdReceipt.Content.ReadFromJsonAsync<ReceiptResponse>())!;

        using var emptyPost = await client.PostAsync($"/api/v1/purchase-receipts/{receipt.Id}/post", null);
        Assert.Equal(HttpStatusCode.Conflict, emptyPost.StatusCode);
        using var added = await client.PostAsJsonAsync($"/api/v1/purchase-receipts/{receipt.Id}/lines",
            new ReceiptLineRequest(ingredient.IngredientId, 3m, 2m));
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        using var posted = await client.PostAsync($"/api/v1/purchase-receipts/{receipt.Id}/post", null);
        Assert.Equal(HttpStatusCode.NoContent, posted.StatusCode);
        using var repeated = await client.PostAsync($"/api/v1/purchase-receipts/{receipt.Id}/post", null);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);

        var balance = await client.GetFromJsonAsync<IngredientBalanceResponse>($"/api/v1/ingredients/{ingredient.IngredientId}");
        Assert.Equal(3m, balance!.AvailableQuantity);
        var movements = await client.GetFromJsonAsync<StockMovementResponse[]>($"/api/v1/ingredients/{ingredient.IngredientId}/movements");
        var movement = Assert.Single(movements!);
        Assert.Equal(inventory.User.Id, movement.ActorId);
        Assert.Equal(receipt.Id, movement.PurchaseReceiptId);
    }

    public async Task DisposeAsync()
    {
        if (host is not null) await host.DisposeAsync();
        if (adminConnection is null) return;
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
