namespace CoffeeShop.Api.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoffeeShop.Api;
using CoffeeShop.Application.Reporting;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
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
            builder.UseTestIdentity().UseSetting("ConnectionStrings:CoffeeShop", connectionString)
                .ConfigureLogging(logging => logging.ClearProviders()));
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
    public async Task GoogleAdmin_ValidatesProviderAndLinkAndPreservesStaffSessionsAndAdminProtection()
    {
        const string clientId = "test-desktop.apps.googleusercontent.com";
        const string verifier = "test-verifier-abcdefghijklmnopqrstuvwxyz-0123456789";
        using var rsa = RSA.Create(2048);
        using var unrelatedRsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "test-provider-key" };
        var unrelatedKey = new RsaSecurityKey(unrelatedRsa) { KeyId = key.KeyId };
        var tokenMetadata = new OpenIdConnectConfiguration();
        tokenMetadata.SigningKeys.Add(key);
        using var legacyClient = host!.CreateClient();
        var legacyAdmin = await LoginAsync(legacyClient, "Admin");
        var legacyStaff = await LoginAsync(legacyClient);
        var links = new[] { "Admin", "inactive-admin", "Cashier", "email-changed" };
        Dictionary<string, Guid> userIds;
        await using (var db = Database())
        {
            await AddUserAsync(db, "inactive-admin", ["Admin"], false);
            await AddUserAsync(db, "unlinked-admin", ["Admin"]);
            await AddUserAsync(db, "email-changed", ["Admin"]);
            userIds = await db.Users.Where(user => links.Contains(user.Username))
                .ToDictionaryAsync(user => user.Username, user => user.Id.Value);
        }

        int exchangeCalls = 0;
        await using var googleHost = host.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("GoogleAdmin:Enabled", "true").UseSetting("GoogleAdmin:ClientId", clientId)
                .UseSetting("GoogleAdmin:ClientSecret", "synthetic-test-only");
            for (var i = 0; i < links.Length; i++)
            {
                var name = links[i];
                var prefix = $"GoogleAdmin:Links:{i}:";
                builder.UseSetting(prefix + "UserId", userIds[name].ToString())
                    .UseSetting(prefix + "Email", name == "email-changed" ? "old-email@example.test" : $"{name}@example.test");
            }
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IConfigurationManager<OpenIdConnectConfiguration>>();
                services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(
                    new StaticConfigurationManager<OpenIdConnectConfiguration>(tokenMetadata));
                services.AddHttpClient("GoogleAdminIdentityProvider").ConfigurePrimaryHttpMessageHandler(() =>
                    new GoogleExchangeHandler(async (request, ct) =>
                    {
                        Interlocked.Increment(ref exchangeCalls);
                        Assert.Equal("https://oauth2.googleapis.com/token", request.RequestUri!.AbsoluteUri);
                        var form = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(ct));
                        Assert.Equal("authorization_code", form["grant_type"].ToString());
                        Assert.Equal(clientId, form["client_id"].ToString());
                        Assert.Equal(verifier, form["code_verifier"].ToString());
                        Assert.Equal(GoogleAdminOAuth.LoopbackRedirect(34567), form["redirect_uri"].ToString());
                        var code = form["code"].ToString();
                        if (code == "outage") return new(HttpStatusCode.ServiceUnavailable);
                        if (code == "replay") return new(HttpStatusCode.BadRequest);
                        var account = links.Contains(code) || code is "unlinked-admin" ? code : "Admin";
                        var email = code switch
                        {
                            "email-changed" => "old-email@example.test",
                            "wrong-email" => "unlinked-admin@example.test",
                            "replacement-google-account" => "ADMIN@example.test",
                            _ => $"{account}@example.test",
                        };
                        var expired = code == "expired";
                        var now = DateTime.UtcNow;
                        var jwt = new JwtSecurityToken(code == "wrong-issuer" ? "https://example.test" : "https://accounts.google.com",
                            code == "wrong-audience" ? "unrelated-client" : clientId, [
                                new("sub", $"google-subject-{code}"), new("email", email),
                                new("email_verified", code == "unverified" ? "false" : "true", ClaimValueTypes.Boolean),
                                new("nonce", code == "wrong-nonce" ? "unrelated-nonce" : GoogleAdminOAuth.Challenge(verifier)),
                            ], now.AddMinutes(-10), expired ? now.AddMinutes(-5) : now.AddMinutes(5),
                            code == "unsigned" ? null : new SigningCredentials(code == "wrong-signature" ? unrelatedKey : key, SecurityAlgorithms.RsaSha256));
                        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { id_token = new JwtSecurityTokenHandler().WriteToken(jwt) }) };
                    }));
            });
        });
        using var client = googleHost.CreateClient();
        const string googlePath = "/api/v1/auth/google";
        using var publicConfig = await client.GetAsync(googlePath + "/config");
        Assert.Equal("no-store", publicConfig.Headers.CacheControl?.ToString());
        Assert.Equal(clientId, (await publicConfig.Content.ReadFromJsonAsync<GoogleAdminConfigResponse>())!.ClientId);
        using var disabledConfig = await legacyClient.GetAsync(googlePath + "/config");
        Assert.Equal(HttpStatusCode.NotFound, disabledConfig.StatusCode);
        var request = new GoogleAdminLoginRequest("Admin", verifier, GoogleAdminOAuth.LoopbackRedirect(34567));
        using var wrongRedirect = await client.PostAsJsonAsync(googlePath, request with { RedirectUri = "https://example.test/oauth/google" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongRedirect.StatusCode);
        Assert.Equal(0, exchangeCalls);
        foreach (var code in new[] { "inactive-admin", "unlinked-admin", "Cashier", "email-changed", "wrong-email", "unverified", "wrong-nonce", "wrong-audience", "wrong-issuer", "expired", "unsigned", "wrong-signature", "replay" })
        {
            using var denied = await client.PostAsJsonAsync(googlePath, request with { Code = code });
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            Assert.Equal("no-store", denied.Headers.CacheControl?.ToString());
            Assert.DoesNotContain("refreshToken", await denied.Content.ReadAsStringAsync());
        }
        using var outage = await client.PostAsJsonAsync(googlePath, request with { Code = "outage" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, outage.StatusCode);
        using var password = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("Admin", Password));
        Assert.Equal(HttpStatusCode.Unauthorized, password.StatusCode);
        using var unlinkedPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("unlinked-admin", Password));
        Assert.Equal(HttpStatusCode.Unauthorized, unlinkedPassword.StatusCode);
        using var legacyRenewal = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(legacyAdmin.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, legacyRenewal.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", legacyAdmin.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", legacyStaff.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/users")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using var staffRenewal = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(legacyStaff.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, staffRenewal.StatusCode);

        using var signedIn = await client.PostAsJsonAsync(googlePath, request);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        var tokens = (await signedIn.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.Equal(userIds["Admin"], tokens.User.Id);
        Assert.Contains("users.manage", tokens.User.Permissions);
        using var replacementSignIn = await client.PostAsJsonAsync(googlePath, request with { Code = "replacement-google-account" });
        Assert.Equal(HttpStatusCode.OK, replacementSignIn.StatusCode);
        Assert.Equal(tokens.User.Id, (await replacementSignIn.Content.ReadFromJsonAsync<TokenResponse>())!.User.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/users")).StatusCode);
        var deactivate = $"/api/v1/users/{tokens.User.Id}/deactivate";
        using var protectedAdmin = await client.PostAsJsonAsync(deactivate, new DeactivateStaffRequest(true));
        Assert.Equal(HttpStatusCode.Conflict, protectedAdmin.StatusCode);
        using var renewed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        var rotated = (await renewed.Content.ReadFromJsonAsync<TokenResponse>())!;
        using var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new RefreshRequest(rotated.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var revoked = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(rotated.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    private sealed class GoogleExchangeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }

    [PostgresFact]
    public async Task GoogleAdminProvisioning_CreatesAnAdminWithoutAUsablePassword()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProvisionStaff:Username"] = "google-only-admin",
            ["ProvisionStaff:Email"] = "google-only-admin@example.test",
            ["ProvisionStaff:FullName"] = "Google-only Admin",
            ["ProvisionStaff:GoogleOnly"] = "true",
            ["ProvisionStaff:Roles"] = "Admin",
        }).Build();
        await StaffProvisioning.RunAsync(host!.Services, configuration, true);
        using var client = host.CreateClient();
        using var denied = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("google-only-admin", Password));
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        await using var db = Database();
        var admin = await db.Users.Include(user => user.UserRoles).ThenInclude(role => role.Role)
            .SingleAsync(user => user.Username == "google-only-admin");
        Assert.True(admin.IsActive);
        Assert.Equal("Admin", Assert.Single(admin.UserRoles).Role!.Code);
    }

    [PostgresFact]
    public async Task Assistant204_EnforcesHttpPermissionsAndPersistsOnlyRedactedReadOnlyOutcomes()
    {
        await using var assistantHost = host!.WithWebHostBuilder(builder => builder.UseSetting("Assistant:Provider", "deterministic"));
        using var client = assistantHost.CreateClient();
        var request = new AssistantAskRequest("Show revenue", new(2026, 11, 1), new(2026, 11, 15), "UTC");
        using var anonymous = await client.PostAsJsonAsync("/api/v1/assistant/ask", request);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await LoginAsync(client)).AccessToken);
        using var forbidden = await client.PostAsJsonAsync("/api/v1/assistant/ask", request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.DoesNotContain("toolsInvoked", await forbidden.Content.ReadAsStringAsync());

        var manager = await LoginAsync(client, "Manager");
        client.DefaultRequestHeaders.Authorization = new("Bearer", manager.AccessToken);
        using var response = await client.PostAsJsonAsync("/api/v1/assistant/ask", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var answer = (await response.Content.ReadFromJsonAsync<AssistantAskResponse>())!;
        Assert.Equal(request.From, answer.From);
        Assert.Equal(request.To, answer.To);
        Assert.Contains("No matching revenue data", answer.Answer);
        Assert.Equal(AssistantTools.Revenue, Assert.Single(answer.ToolsInvoked).Tool);

        using var invalid = await client.PostAsJsonAsync("/api/v1/assistant/ask", request with { To = new(2026, 10, 1) });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var refusal = await client.PostAsJsonAsync("/api/v1/assistant/ask", request with { Question = "Cancel the last order" });
        Assert.Empty((await refusal.Content.ReadFromJsonAsync<AssistantAskResponse>())!.ToolsInvoked);
        using var unsupported = await client.PostAsJsonAsync("/api/v1/assistant/ask", request with { Question = "Weather? token=synthetic-private-value" });
        Assert.Empty((await unsupported.Content.ReadFromJsonAsync<AssistantAskResponse>())!.ToolsInvoked);

        await using var db = Database();
        var audits = await db.AiInteractionAudits.ToArrayAsync();
        Assert.Equal(4, audits.Length);
        Assert.All(audits, audit => Assert.Equal(new EntityId(manager.User.Id), audit.UserId));
        Assert.Contains(audits, audit => audit.Outcome == "refused");
        Assert.Contains(audits, audit => audit.Outcome == "unsupported" && audit.Question.Contains("[REDACTED]"));
        Assert.All(audits, audit =>
        {
            Assert.DoesNotContain("synthetic-private-value", audit.Question);
            Assert.DoesNotContain(manager.AccessToken, audit.Question);
        });
        Assert.Empty(await db.Orders.ToArrayAsync());
        Assert.Empty(await db.StockMovements.ToArrayAsync());
    }

    [PostgresFact]
    public async Task MobileDevices_RequireAuthenticationAndEnforceOwnershipRotationAndRevocation()
    {
        using var owner = host!.CreateClient();
        using var other = host.CreateClient();
        var request = new MobileDeviceRequest("android", "token-a");
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.PostAsJsonAsync("/api/v1/mobile-devices/", request)).StatusCode);
        owner.DefaultRequestHeaders.Authorization = new("Bearer", (await LoginAsync(owner)).AccessToken);
        other.DefaultRequestHeaders.Authorization = new("Bearer", (await LoginAsync(other, "Barista")).AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/mobile-devices/", new MobileDeviceRequest("android", " "))).StatusCode);
        using var created = await owner.PostAsJsonAsync("/api/v1/mobile-devices/", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<MobileDeviceResponse>())!.Id;
        var path = $"/api/v1/mobile-devices/{id}";
        using var retried = await owner.PostAsJsonAsync("/api/v1/mobile-devices/", request);
        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
        Assert.Equal(id, (await retried.Content.ReadFromJsonAsync<MobileDeviceResponse>())!.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsJsonAsync("/api/v1/mobile-devices/", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(path, new MobileDeviceRequest("ios", "token-b"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync(path, new MobileDeviceRequest("ios", "token-b"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await other.PostAsJsonAsync("/api/v1/mobile-devices/", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync("/api/v1/mobile-devices/", new MobileDeviceRequest("ios", "token-b"))).StatusCode);
    }

    [PostgresFact]
    public async Task InvalidDeviceTokenCleanup_RevokesOnlyMatchingActiveRegistration()
    {
        using var client = host!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await LoginAsync(client)).AccessToken);
        var response = await client.PostAsJsonAsync("/api/v1/mobile-devices/", new MobileDeviceRequest("android", "invalid-token"));
        var id = (await response.Content.ReadFromJsonAsync<MobileDeviceResponse>())!.Id;
        await using var db = Database();
        var cleanup = new InvalidMobileTokenCleanup(db);
        Assert.False(await cleanup.RevokeAsync("unknown-token"));
        Assert.True(await cleanup.RevokeAsync("invalid-token"));
        Assert.False(await cleanup.RevokeAsync("invalid-token"));
        db.ChangeTracker.Clear();
        Assert.NotNull((await db.MobileDeviceRegistrations.SingleAsync(device => device.Id == new EntityId(id))).RevokedAt);

        var next = await client.PostAsJsonAsync("/api/v1/mobile-devices/", new MobileDeviceRequest("android", "old-token"));
        var nextId = (await next.Content.ReadFromJsonAsync<MobileDeviceResponse>())!.Id;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/mobile-devices/{nextId}", new MobileDeviceRequest("android", "new-token"))).StatusCode);
        Assert.False(await cleanup.RevokeAsync("old-token"));
        db.ChangeTracker.Clear();
        Assert.Null((await db.MobileDeviceRegistrations.SingleAsync(device => device.Id == new EntityId(nextId))).RevokedAt);
    }

    [PostgresFact]
    public async Task StaffPush_UsesCurrentPermissionsAndOnlyOpaqueTargets()
    {
        const string orderId = "11111111-1111-1111-1111-111111111181";
        const string discountId = "22222222-2222-2222-2222-222222222181";
        const string ingredientId = "33333333-3333-3333-3333-333333333181";
        const string sql = """
            INSERT INTO sales.orders (id, order_number, channel, status, confirmed_at, created_by)
              SELECT '11111111-1111-1111-1111-111111111181', 'PUSH-TEST', 'delivery', 'confirmed', now(), id
              FROM identity.users WHERE username = 'Cashier';
            INSERT INTO sales.discounts (id, order_id, type, value, reason, requested_by)
              SELECT '22222222-2222-2222-2222-222222222181', '11111111-1111-1111-1111-111111111181', 'fixed', 1, 'private reason', id
              FROM identity.users WHERE username = 'Cashier';
            INSERT INTO inventory.ingredients (id, name, base_unit) VALUES ('33333333-3333-3333-3333-333333333181', 'Private ingredient name', 'g');
            INSERT INTO sales.deliveries (order_id, recipient_name, contact_number, address_line, assigned_staff_id, status)
              SELECT '11111111-1111-1111-1111-111111111181', 'private recipient', '123456789', 'private address', id, 'assigned'
              FROM identity.users WHERE username = 'DeliveryStaff';
            """;
        await using (var db = Database()) await db.Database.ExecuteSqlRawAsync(sql);
        foreach (var role in new[] { "Manager", "Barista", "InventoryStaff", "DeliveryStaff", "Cashier" })
        {
            using var client = host!.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", (await LoginAsync(client, role)).AccessToken);
            using var response = await client.PostAsJsonAsync("/api/v1/mobile-devices", new MobileDeviceRequest("android", $"fixture-{role}"));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        await using (var db = Database()) await AddUserAsync(db, "other-courier", ["DeliveryStaff"]);
        using (var client = host!.CreateClient())
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", (await LoginAsync(client, "other-courier")).AccessToken);
            using var response = await client.PostAsJsonAsync("/api/v1/mobile-devices", new MobileDeviceRequest("android", "fixture-other-courier"));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        await using var scope = host!.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<MobilePushDispatch>();
        var provider = new DeterministicPushProvider();
        await dispatcher.DispatchAsync(provider, new("discount", Guid.Parse(orderId), DateTimeOffset.UtcNow), CancellationToken.None);
        await dispatcher.DispatchAsync(provider, new("delivery", Guid.Parse(orderId), DateTimeOffset.UtcNow), CancellationToken.None);
        await dispatcher.DispatchAsync(provider, new("inventory", Guid.Parse(ingredientId), DateTimeOffset.UtcNow), CancellationToken.None);
        await dispatcher.DispatchAsync(provider, new("order", Guid.Parse(orderId), DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.Contains(("fixture-Manager", new StaffPushTarget("approval", Guid.Parse(discountId))), provider.Deliveries);
        Assert.Equal(("fixture-DeliveryStaff", new StaffPushTarget("assignment", Guid.Parse(orderId))),
            Assert.Single(provider.Deliveries, delivery => delivery.Target.Kind == "assignment"));
        Assert.Contains(("fixture-InventoryStaff", new StaffPushTarget("low-stock", Guid.Parse(ingredientId))), provider.Deliveries);
        Assert.Contains(("fixture-Barista", new StaffPushTarget("urgent-order", Guid.Parse(orderId))), provider.Deliveries);
        Assert.DoesNotContain(provider.Deliveries, delivery => delivery.Destination == "fixture-Cashier");
        await using var check = Database();
        Assert.NotNull((await check.MobileDeviceRegistrations.SingleAsync(device => device.Token == "fixture-InventoryStaff")).RevokedAt);
    }

    private sealed class DeterministicPushProvider : IMobilePushProvider
    {
        public List<(string Destination, StaffPushTarget Target)> Deliveries { get; } = [];

        public Task<PushDeliveryStatus> SendAsync(string destination, StaffPushNotification notification, CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(notification, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            Assert.Equal(new[] { "body", "data", "title" }, document.RootElement.EnumerateObject().Select(property => property.Name).Order());
            Assert.Equal(new[] { "kind", "recordId" }, document.RootElement.GetProperty("data").EnumerateObject().Select(property => property.Name).Order());
            Assert.Equal("Coffee Shop Staff", notification.Title);
            Assert.Equal("Open the app to review a staff update.", notification.Body);
            Deliveries.Add((destination, notification.Data));
            return Task.FromResult(destination == "fixture-InventoryStaff" ? PushDeliveryStatus.InvalidDevice : PushDeliveryStatus.Sent);
        }
    }

    [PostgresFact]
    public async Task OperationalHub_RequiresAuthenticationAndRejectsClientGroupSelection()
    {
        using var client = host!.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync("/api/v1/operational-events/negotiate?negotiateVersion=1", null)).StatusCode);
        var token = (await LoginAsync(client, "Barista")).AccessToken;
        var socketClient = host.Server.CreateWebSocketClient();
        socketClient.ConfigureRequest = request => request.Headers.Authorization = $"Bearer {token}";
        using var socket = await socketClient.ConnectAsync(new Uri("ws://localhost/api/v1/operational-events"), CancellationToken.None);
        async Task Send(string value) => await socket.SendAsync(Encoding.UTF8.GetBytes(value + '\u001e'), WebSocketMessageType.Text, true, CancellationToken.None);
        async Task<string> Receive()
        {
            var bytes = new byte[8192];
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = await socket.ReceiveAsync(bytes, timeout.Token);
            return Encoding.UTF8.GetString(bytes, 0, result.Count);
        }
        await Send("{\"protocol\":\"json\",\"version\":1}");
        Assert.Contains("{}", await Receive());
        await Send("{\"type\":1,\"invocationId\":\"1\",\"target\":\"JoinGroup\",\"arguments\":[\"permission:users.manage\"]}");
        var reply = await Receive();
        Assert.Contains("error", reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1", reply);
    }

    [PostgresFact]
    public async Task OperationalNotifications_ArriveOnlyAfterCommitAndContainIdentifiersOnly()
    {
        await using var listener = new NpgsqlConnection(connectionString);
        await listener.OpenAsync();
        var notifications = new List<string>();
        listener.Notification += (_, args) => notifications.Add(args.Payload);
        await using (var command = new NpgsqlCommand("LISTEN coffeeshop_operational_events", listener)) await command.ExecuteNonQueryAsync();
        await using var writer = new NpgsqlConnection(connectionString);
        await writer.OpenAsync();
        await using (var tx = await writer.BeginTransactionAsync())
        {
            await using var update = new NpgsqlCommand("UPDATE identity.users SET full_name = 'changed private name' WHERE username = 'Cashier'", writer, tx);
            await update.ExecuteNonQueryAsync();
            Assert.False(await listener.WaitAsync(300));
            await tx.RollbackAsync();
        }
        Assert.False(await listener.WaitAsync(300));
        Assert.Empty(notifications);
        await using (var tx = await writer.BeginTransactionAsync())
        {
            await using var update = new NpgsqlCommand("UPDATE identity.users SET full_name = 'changed private name' WHERE username = 'Cashier'", writer, tx);
            await update.ExecuteNonQueryAsync();
            Assert.False(await listener.WaitAsync(300));
            await tx.CommitAsync();
        }
        Assert.True(await listener.WaitAsync(5000));
        using var payload = JsonDocument.Parse(Assert.Single(notifications));
        Assert.Equal(new[] { "id", "type", "updatedAt" }, payload.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal("account", payload.RootElement.GetProperty("type").GetString());
        Assert.True(Guid.TryParse(payload.RootElement.GetProperty("id").GetString(), out _));
        Assert.DoesNotContain("private name", notifications[0]);
    }

    [PostgresFact]
    public async Task OperationalNotifications_CoverEveryMappedBusinessArea()
    {
        await using var listener = new NpgsqlConnection(connectionString);
        await listener.OpenAsync();
        var notifications = new List<string>();
        listener.Notification += (_, args) => notifications.Add(args.Payload);
        await using (var listen = new NpgsqlCommand("LISTEN coffeeshop_operational_events", listener)) await listen.ExecuteNonQueryAsync();
        await using var writer = new NpgsqlConnection(connectionString);
        await writer.OpenAsync();
        await using (var tx = await writer.BeginTransactionAsync())
        {
            const string sql = """
                INSERT INTO sales.orders (id, order_number, channel, created_by)
                  SELECT '11111111-1111-1111-1111-111111111111', 'EVENT-TEST', 'delivery', id FROM identity.users WHERE username = 'Cashier';
                INSERT INTO sales.discounts (order_id, type, value, reason, requested_by)
                  SELECT '11111111-1111-1111-1111-111111111111', 'fixed', 1, 'private reason', id FROM identity.users WHERE username = 'Cashier';
                INSERT INTO sales.payments (order_id, method, amount, recorded_by)
                  SELECT '11111111-1111-1111-1111-111111111111', 'cash', 1, id FROM identity.users WHERE username = 'Cashier';
                INSERT INTO inventory.ingredients (name, base_unit) VALUES ('Event Beans', 'g');
                INSERT INTO sales.deliveries (order_id, recipient_name, contact_number, address_line)
                  VALUES ('11111111-1111-1111-1111-111111111111', 'private recipient', '123456789', 'private address');
                UPDATE identity.users SET full_name = 'private name' WHERE username = 'Cashier';
                """;
            await using var command = new NpgsqlCommand(sql, writer, tx);
            await command.ExecuteNonQueryAsync();
            Assert.False(await listener.WaitAsync(300));
            await tx.CommitAsync();
        }
        while (notifications.Count < 6 && await listener.WaitAsync(5000)) { }
        var types = notifications.Select(payload =>
        {
            using var json = JsonDocument.Parse(payload);
            Assert.Equal(new[] { "id", "type", "updatedAt" }, json.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
            Assert.True(Guid.TryParse(json.RootElement.GetProperty("id").GetString(), out _));
            Assert.DoesNotContain("private", payload, StringComparison.OrdinalIgnoreCase);
            return json.RootElement.GetProperty("type").GetString();
        }).Order().ToArray();
        Assert.Equal(new[] { "account", "delivery", "discount", "inventory", "order", "payment" }, types);
    }

    [PostgresFact]
    public async Task OperationalEvents_AreDeliveredOnlyToCurrentlyAuthorizedUsers()
    {
        using var http = host!.CreateClient();
        var admin = (await LoginAsync(http, "Admin")).AccessToken;
        var barista = (await LoginAsync(http, "Barista")).AccessToken;
        async Task<WebSocket> Connect(string token)
        {
            var client = host.Server.CreateWebSocketClient();
            client.ConfigureRequest = request => request.Headers.Authorization = $"Bearer {token}";
            var socket = await client.ConnectAsync(new Uri("ws://localhost/api/v1/operational-events"), CancellationToken.None);
            await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
            var buffer = new byte[4096];
            await socket.ReceiveAsync(buffer, CancellationToken.None);
            return socket;
        }
        using var adminSocket = await Connect(admin);
        using var baristaSocket = await Connect(barista);
        // Let the background listener establish LISTEN before producing the committed update.
        await Task.Delay(500);
        await using (var db = Database())
            await db.Database.ExecuteSqlRawAsync("UPDATE identity.users SET full_name = 'Private Updated Name' WHERE username = 'Cashier'");
        var bytes = new byte[4096];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = await adminSocket.ReceiveAsync(bytes, timeout.Token);
        var message = Encoding.UTF8.GetString(bytes, 0, received.Count);
        Assert.Contains("OperationalEvent", message);
        Assert.Contains("account", message);
        Assert.DoesNotContain("Private Updated Name", message);
        using var noMessage = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await baristaSocket.ReceiveAsync(bytes, noMessage.Token));

        // Existing connections must stop receiving privileged events after their role is removed.
        await using (var db = Database())
            await db.Database.ExecuteSqlRawAsync("DELETE FROM identity.user_roles WHERE user_id = (SELECT id FROM identity.users WHERE username = 'Admin')");
        using var ownChangeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await adminSocket.ReceiveAsync(bytes, ownChangeTimeout.Token); // the user's own account-change hint
        await using (var db = Database())
            await db.Database.ExecuteSqlRawAsync("UPDATE identity.users SET full_name = 'Another Private Name' WHERE username = 'Cashier'");
        using var revokedTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await adminSocket.ReceiveAsync(bytes, revokedTimeout.Token));
    }

    [PostgresFact]
    public async Task DeliveryEvents_AreScopedToTheAssignedCourier()
    {
        Guid orderId = Guid.NewGuid();
        await using (var db = Database())
        {
            await AddUserAsync(db, "courier2", ["DeliveryStaff"]);
            var courier = await db.Users.SingleAsync(u => u.Username == "DeliveryStaff");
            var actor = await db.Users.SingleAsync(u => u.Username == "Cashier");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO sales.orders (id, order_number, channel, created_by) VALUES ({orderId}, 'EVENT-DELIVERY', 'delivery', {actor.Id.Value})");
            var deliveryId = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO sales.deliveries (id, order_id, recipient_name, contact_number, address_line, status, assigned_staff_id) VALUES ({deliveryId}, {orderId}, 'Private Name', '123456789', 'Private Address', 'assigned', {courier.Id.Value})");
        }
        using var http = host!.CreateClient();
        var assignedToken = (await LoginAsync(http, "DeliveryStaff")).AccessToken;
        var otherToken = (await LoginAsync(http, "courier2")).AccessToken;
        async Task<WebSocket> Connect(string token)
        {
            var client = host.Server.CreateWebSocketClient();
            client.ConfigureRequest = request => request.Headers.Authorization = $"Bearer {token}";
            var socket = await client.ConnectAsync(new Uri("ws://localhost/api/v1/operational-events"), CancellationToken.None);
            await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
            await socket.ReceiveAsync(new byte[4096], CancellationToken.None);
            return socket;
        }
        using var assigned = await Connect(assignedToken);
        using var other = await Connect(otherToken);
        await Task.Delay(500);
        await using (var db = Database())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.deliveries SET address_line = 'New Private Address' WHERE order_id = {orderId}");
        var buffer = new byte[4096];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await assigned.ReceiveAsync(buffer, timeout.Token);
        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Assert.Contains(orderId.ToString(), message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("delivery", message);
        Assert.DoesNotContain("Private", message);
        using var noMessage = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await other.ReceiveAsync(buffer, noMessage.Token));
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
    public async Task StaffAdministration_RequiresPermissionAndNeverReturnsCredentialsOrMetadata()
    {
        using var client = host!.CreateClient();
        var cashier = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", cashier.AccessToken);
        using var forbiddenUsers = await client.GetAsync("/api/v1/users?search=Cashier");
        using var forbiddenEvents = await client.GetAsync("/api/v1/security-events");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenUsers.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenEvents.StatusCode);
        Assert.Equal("application/problem+json", forbiddenUsers.Content.Headers.ContentType?.MediaType);

        var admin = await LoginAsync(client, "Admin");
        client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);
        using var users = await client.GetAsync("/api/v1/users?search=Cashier&limit=5");
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
        var accountJson = await users.Content.ReadAsStringAsync();
        Assert.Contains("Cashier", accountJson);
        Assert.DoesNotContain(Password, accountJson);
        Assert.DoesNotContain(admin.AccessToken, accountJson);
        Assert.DoesNotContain(admin.RefreshToken, accountJson);
        var accounts = await users.Content.ReadFromJsonAsync<StaffAccountResponse[]>();
        var account = Assert.Single(accounts!);
        using var detail = await client.GetAsync($"/api/v1/users/{account.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailJson = await detail.Content.ReadAsStringAsync();
        Assert.Contains(account.Username, detailJson);
        Assert.DoesNotContain(Password, detailJson);
        Assert.DoesNotContain(admin.AccessToken, detailJson);
        Assert.DoesNotContain(admin.RefreshToken, detailJson);

        await using (var db = Database())
        {
            db.SecurityAuditEvents.Add(new SecurityAuditEvent(EntityId.New(), "safe_test_event",
                new UtcTimestamp(DateTimeOffset.UtcNow), "success", null, null,
                System.Text.Json.JsonSerializer.Serialize(new { token = admin.AccessToken, password = Password })));
            await db.SaveChangesAsync();
        }
        using var events = await client.GetAsync("/api/v1/security-events?eventType=safe_test_event");
        Assert.Equal(HttpStatusCode.OK, events.StatusCode);
        var eventJson = await events.Content.ReadAsStringAsync();
        Assert.Contains("safe_test_event", eventJson);
        Assert.DoesNotContain("metadata", eventJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, eventJson);
        Assert.DoesNotContain(admin.AccessToken, eventJson);
        Assert.DoesNotContain(admin.RefreshToken, eventJson);
    }

    [PostgresFact]
    public async Task StaffCreation_RequiresAdministratorAndRejectsInvalidAndDuplicateIdentityWithoutPartialWrites()
    {
        using var client = host!.CreateClient();
        var input = new
        {
            fullName = "New Cashier",
            username = "new.cashier",
            email = "new.cashier@example.test",
            phone = "+84901234567",
            role = "Cashier",
            password = "test-only-new-cashier-password"
        };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/users", input)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await LoginAsync(client)).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/users", input)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await LoginAsync(client, "Admin")).AccessToken);

        using var invalidRole = await client.PostAsJsonAsync("/api/v1/users", new
        {
            input.fullName,
            input.username,
            input.email,
            input.phone,
            role = "Owner",
            input.password
        });
        using var invalidPassword = await client.PostAsJsonAsync("/api/v1/users", new
        {
            input.fullName,
            input.username,
            input.email,
            input.phone,
            input.role,
            password = "short"
        });
        using var invalidContact = await client.PostAsJsonAsync("/api/v1/users", new
        {
            input.fullName,
            input.username,
            email = "bad-address",
            input.phone,
            input.role,
            input.password
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPassword.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidContact.StatusCode);

        using var created = await client.PostAsJsonAsync("/api/v1/users", input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var duplicateUsername = await client.PostAsJsonAsync("/api/v1/users", new
        {
            input.fullName,
            username = "NEW.CASHIER",
            email = "other@example.test",
            input.phone,
            input.role,
            input.password
        });
        using var duplicateEmail = await client.PostAsJsonAsync("/api/v1/users", new
        {
            input.fullName,
            username = "other.user",
            email = "NEW.CASHIER@EXAMPLE.TEST",
            input.phone,
            input.role,
            input.password
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateUsername.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateEmail.StatusCode);
        await using var db = Database();
        Assert.Single(await db.Users.Where(user => user.Username.ToLower() == "new.cashier").ToArrayAsync());
        Assert.False(await db.Users.AnyAsync(user => user.Username == "other.user"));
    }

    [PostgresFact]
    public async Task StaffCreation_HashesPasswordAuditsActorAndAllowsAssignedRoleToSignIn()
    {
        using var client = host!.CreateClient();
        var admin = await LoginAsync(client, "Admin");
        client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);
        const string password = "test-only-created-barista-password";
        using var created = await client.PostAsJsonAsync("/api/v1/users", new
        {
            fullName = "Created Barista",
            username = "created.barista",
            email = "created.barista@example.test",
            phone = (string?)null,
            role = "Barista",
            password,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("no-store", created.Headers.CacheControl?.ToString());
        var responseBody = await created.Content.ReadAsStringAsync();
        Assert.DoesNotContain(password, responseBody);
        Assert.DoesNotContain("passwordHash", responseBody, StringComparison.OrdinalIgnoreCase);
        var account = (await created.Content.ReadFromJsonAsync<StaffAccountResponse>())!;
        Assert.Equal(["Barista"], account.Roles);

        await using (var db = Database())
        {
            var user = await db.Users.SingleAsync(item => item.Id == new EntityId(account.Id));
            Assert.NotEqual(password, user.PasswordHash);
            var audit = await db.SecurityAuditEvents.SingleAsync(item => item.UserId == user.Id && item.EventType == "account_created");
            Assert.Equal("success", audit.Outcome);
            Assert.Contains(admin.User.Id.ToString(), audit.Metadata);
            Assert.DoesNotContain(password, audit.Metadata);
        }
        client.DefaultRequestHeaders.Authorization = null;
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(account.Username, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.Equal(["Barista"], session.User.Roles);
        Assert.Contains("orders.fulfill", session.User.Permissions);
        Assert.DoesNotContain("users.manage", session.User.Permissions);
    }

    [PostgresFact]
    public async Task StaffAdministration_UpdatesProfileAndRolesWithActorAudits()
    {
        using var client = host!.CreateClient();
        var admin = await LoginAsync(client, "Admin");
        client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);
        using var created = await client.PostAsJsonAsync("/api/v1/users", new
        {
            fullName = "Original Cashier",
            username = "original.cashier",
            email = "original.cashier@example.test",
            phone = (string?)null,
            role = "Cashier",
            password = "test-only-account-lifecycle-password",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var account = (await created.Content.ReadFromJsonAsync<StaffAccountResponse>())!;

        var cashier = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", cashier.AccessToken);
        using var forbidden = await client.PutAsJsonAsync($"/api/v1/users/{account.Id}", new
        {
            username = "updated.cashier",
            fullName = "Updated Cashier",
            email = "updated.cashier@example.test",
            phone = "+84901234567",
            roles = new[] { "Barista" },
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);
        using var updated = await client.PutAsJsonAsync($"/api/v1/users/{account.Id}", new
        {
            username = " updated.cashier ",
            fullName = " Updated Cashier ",
            email = " UPDATED.CASHIER@EXAMPLE.TEST ",
            phone = " +84901234567 ",
            roles = new[] { "Barista" },
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("no-store", updated.Headers.CacheControl?.ToString());
        var response = (await updated.Content.ReadFromJsonAsync<StaffAccountResponse>())!;
        Assert.Equal("updated.cashier", response.Username);
        Assert.Equal("Updated Cashier", response.FullName);
        Assert.Equal("updated.cashier@example.test", response.Email);
        Assert.Equal("+84901234567", response.Phone);
        Assert.Equal(["Barista"], response.Roles);
        Assert.DoesNotContain("passwordHash", await updated.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        client.DefaultRequestHeaders.Authorization = new("Bearer", cashier.AccessToken);
        using var refreshedAccess = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, refreshedAccess.StatusCode);
        var updatedSession = (await refreshedAccess.Content.ReadFromJsonAsync<StaffSession>())!;
        Assert.Equal(["Barista"], updatedSession.Roles);

        client.DefaultRequestHeaders.Authorization = null;
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("updated.cashier", "test-only-account-lifecycle-password"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.Equal(["Barista"], session.User.Roles);
        Assert.DoesNotContain("users.manage", session.User.Permissions);

        await using var db = Database();
        var updateAudit = await db.SecurityAuditEvents.SingleAsync(item => item.UserId == new EntityId(account.Id) &&
            item.EventType == "account_updated");
        var roleAudit = await db.SecurityAuditEvents.SingleAsync(item => item.UserId == new EntityId(account.Id) &&
            item.EventType == "role_changed");
        Assert.Contains(admin.User.Id.ToString(), updateAudit.Metadata);
        Assert.Contains(admin.User.Id.ToString(), roleAudit.Metadata);
        Assert.Contains("Cashier", roleAudit.Metadata);
        Assert.Contains("Barista", roleAudit.Metadata);
        Assert.DoesNotContain("test-only-account-lifecycle-password", updateAudit.Metadata);
        Assert.DoesNotContain(admin.AccessToken, roleAudit.Metadata);
    }

    [PostgresFact]
    public async Task StaffAdministration_RejectsDuplicateAndInvalidProfileOrRolesWithoutPartialWrites()
    {
        using var client = host!.CreateClient();
        var admin = await LoginAsync(client, "Admin");
        client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);
        var target = await LoginAsync(client);
        using var duplicate = await client.PutAsJsonAsync($"/api/v1/users/{target.User.Id}", new
        {
            username = "Manager",
            fullName = "Changed Cashier",
            email = "changed.cashier@example.test",
            phone = (string?)null,
            roles = new[] { "Barista" },
        });
        using var invalidRole = await client.PutAsJsonAsync($"/api/v1/users/{target.User.Id}", new
        {
            username = "cashier",
            fullName = "Changed Cashier",
            email = "changed.cashier@example.test",
            phone = (string?)null,
            roles = new[] { "Owner" },
        });
        using var emptyRoles = await client.PutAsJsonAsync($"/api/v1/users/{target.User.Id}", new
        {
            username = "cashier",
            fullName = "Changed Cashier",
            email = "changed.cashier@example.test",
            phone = (string?)null,
            roles = Array.Empty<string>(),
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyRoles.StatusCode);

        await using var db = Database();
        var unchanged = await db.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .SingleAsync(item => item.Id == new EntityId(target.User.Id));
        Assert.Equal("Cashier", unchanged.Username);
        Assert.Equal("Cashier", unchanged.FullName);
        Assert.Equal("cashier@example.test", unchanged.Email);
        Assert.Equal(["Cashier"], unchanged.UserRoles.Select(item => item.Role!.Code).ToArray());
        Assert.False(await db.SecurityAuditEvents.AnyAsync(item => item.UserId == unchanged.Id &&
            (item.EventType == "account_updated" || item.EventType == "role_changed")));
    }

    [PostgresFact]
    public async Task StaffAdministration_RejectsDemotingFinalAdminWithoutChangingRolesOrSessions()
    {
        using var client = host!.CreateClient();
        var admin = await LoginAsync(client, "Admin");
        client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);
        using var response = await client.PutAsJsonAsync($"/api/v1/users/{admin.User.Id}", new
        {
            username = "Admin",
            fullName = "Admin",
            email = "admin@example.test",
            phone = (string?)null,
            roles = new[] { "Manager" },
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var db = Database();
        var unchanged = await db.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .SingleAsync(item => item.Id == new EntityId(admin.User.Id));
        Assert.True(unchanged.IsActive);
        Assert.Equal(["Admin"], unchanged.UserRoles.Select(item => item.Role!.Code).ToArray());
        Assert.False(await db.SecurityAuditEvents.AnyAsync(item => item.UserId == unchanged.Id &&
            item.EventType == "role_changed"));
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(admin.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [PostgresFact]
    public async Task StaffAdministration_CannotUnlinkOrDemoteFinalGoogleLinkedAdministrator()
    {
        using var client = host!.CreateClient();
        var admin = await LoginAsync(client, "Admin");
        await using var db = Database();
        var google = new GoogleAdminOptions
        {
            Enabled = true,
            ClientId = "lifecycle-test.apps.googleusercontent.com",
            ClientSecret = "synthetic-test-only",
            Links = [new GoogleAdminLink { UserId = admin.User.Id, Email = "admin@example.test" }],
        };
        var service = new StaffAdministrationService(db, TimeProvider.System, google);
        var unlinked = await service.UpdateAsync(admin.User.Id,
            new UpdateStaffRequest("Admin", "Admin", "unlinked-admin@example.test", null, ["Admin"]),
            new EntityId(admin.User.Id), null, CancellationToken.None);
        var demoted = await service.UpdateAsync(admin.User.Id,
            new UpdateStaffRequest("Admin", "Admin", "admin@example.test", null, ["Manager"]),
            new EntityId(admin.User.Id), null, CancellationToken.None);
        Assert.False(unlinked.IsSuccess);
        Assert.False(demoted.IsSuccess);

        var unchanged = await db.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .SingleAsync(item => item.Id == new EntityId(admin.User.Id));
        Assert.Equal("admin@example.test", unchanged.Email);
        Assert.Equal(["Admin"], unchanged.UserRoles.Select(item => item.Role!.Code).ToArray());
        Assert.False(await db.SecurityAuditEvents.AnyAsync(item => item.UserId == unchanged.Id &&
            (item.EventType == "account_updated" || item.EventType == "role_changed")));
    }

    [PostgresFact]
    public async Task StaffAdministration_RequiresConfirmationToReactivateAndDoesNotRestoreOldSessions()
    {
        using var client = host!.CreateClient();
        var admin = await LoginAsync(client, "Admin");
        client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);
        var target = await LoginAsync(client, "Manager");
        using var deactivated = await client.PostAsJsonAsync($"/api/v1/users/{target.User.Id}/deactivate",
            new DeactivateStaffRequest(true));
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        using var unconfirmed = await client.PostAsJsonAsync($"/api/v1/users/{target.User.Id}/reactivate",
            new { confirm = false });
        Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode);
        using var reactivated = await client.PostAsJsonAsync($"/api/v1/users/{target.User.Id}/reactivate",
            new { confirm = true });
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        var response = (await reactivated.Content.ReadFromJsonAsync<StaffAccountResponse>())!;
        Assert.True(response.IsActive);
        Assert.Null(response.DeactivatedAt);

        await using var db = Database();
        Assert.True(await db.SecurityAuditEvents.AnyAsync(item => item.UserId == new EntityId(target.User.Id) &&
            item.EventType == "account_reactivated" && item.Metadata != null && item.Metadata.Contains(admin.User.Id.ToString())));
        using var oldRefresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(target.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
        using var managerLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("Manager", Password));
        Assert.Equal(HttpStatusCode.OK, managerLogin.StatusCode);
    }

    [PostgresFact]
    public async Task StaffAdministration_ProtectsFinalAdministratorWithoutChangingState()
    {
        using var client = host!.CreateClient();
        var admin = await LoginAsync(client, "Admin");
        client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);
        using var response = await client.PostAsJsonAsync($"/api/v1/users/{admin.User.Id}/deactivate",
            new DeactivateStaffRequest(true));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var db = Database();
        var user = await db.Users.SingleAsync(item => item.Id == new EntityId(admin.User.Id));
        Assert.True(user.IsActive);
        Assert.Null(user.DeactivatedAt);
        Assert.False(await db.SecurityAuditEvents.AnyAsync(item => item.UserId == user.Id &&
            item.EventType == "account_deactivated"));
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(admin.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [PostgresFact]
    public async Task StaffAdministration_DeactivationRequiresConfirmationAndRevokesEverySession()
    {
        using var client = host!.CreateClient();
        var target = await LoginAsync(client, "Manager");
        var admin = await LoginAsync(client, "Admin");
        client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);
        using var unconfirmed = await client.PostAsJsonAsync($"/api/v1/users/{target.User.Id}/deactivate",
            new DeactivateStaffRequest(false));
        Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode);
        await using (var db = Database())
        {
            var unchanged = await db.Users.SingleAsync(item => item.Id == new EntityId(target.User.Id));
            Assert.True(unchanged.IsActive);
            Assert.Null(unchanged.DeactivatedAt);
        }

        using var deactivated = await client.PostAsJsonAsync($"/api/v1/users/{target.User.Id}/deactivate",
            new DeactivateStaffRequest(true));
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        var response = await deactivated.Content.ReadFromJsonAsync<StaffAccountResponse>();
        Assert.NotNull(response);
        Assert.False(response.IsActive);
        var serializedResponse = System.Text.Json.JsonSerializer.Serialize(response);
        Assert.DoesNotContain("password", serializedResponse, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(target.AccessToken, serializedResponse);
        Assert.DoesNotContain(target.RefreshToken, serializedResponse);

        await using (var db = Database())
        {
            var user = await db.Users.SingleAsync(item => item.Id == new EntityId(target.User.Id));
            Assert.False(user.IsActive);
            Assert.NotNull(user.DeactivatedAt);
            Assert.All(await db.RefreshTokens.Where(item => item.UserId == user.Id).ToArrayAsync(),
                token => Assert.NotNull(token.RevokedAt));
            Assert.True(await db.SecurityAuditEvents.AnyAsync(item => item.UserId == user.Id &&
                item.EventType == "account_deactivated"));
        }

        client.DefaultRequestHeaders.Authorization = new("Bearer", target.AccessToken);
        using var me = await client.GetAsync("/api/v1/auth/me");
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(target.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
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
    public async Task MobileP0Queries_UsePagesFiltersPermissionsAndEmptyResults()
    {
        using var client = host!.CreateClient();
        var barista = await LoginAsync(client, "Barista");
        client.DefaultRequestHeaders.Authorization = new("Bearer", barista.AccessToken);
        var tickets = await client.GetFromJsonAsync<PageResponse<PreparationTicketResponse>>(
            "/api/v1/preparation-tickets?status=confirmed,preparing&page=1&pageSize=2");
        Assert.Empty(tickets!.Items);
        Assert.Equal(0, tickets.TotalCount);
        Assert.Equal(2, tickets.PageSize);
        using var invalidStatus = await client.GetAsync("/api/v1/preparation-tickets?status=ready");
        Assert.Equal(HttpStatusCode.BadRequest, invalidStatus.StatusCode);
        using var missing = await client.GetAsync($"/api/v1/preparation-tickets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var manager = await LoginAsync(client, "Manager");
        client.DefaultRequestHeaders.Authorization = new("Bearer", manager.AccessToken);
        var pending = await client.GetFromJsonAsync<PageResponse<PendingDiscountResponse>>("/api/v1/discounts/pending");
        Assert.Empty(pending!.Items);
        using var badPage = await client.GetAsync("/api/v1/discounts/pending?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);
        using var forbiddenCourier = await client.GetAsync("/api/v1/courier/assignments");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCourier.StatusCode);

        var inventory = await LoginAsync(client, "InventoryStaff");
        client.DefaultRequestHeaders.Authorization = new("Bearer", inventory.AccessToken);
        var balances = await client.GetFromJsonAsync<PageResponse<IngredientBalanceResponse>>("/api/v1/ingredients");
        Assert.Empty(balances!.Items);
        using var tooLarge = await client.GetAsync("/api/v1/ingredients/low-stock?pageSize=201");
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
        using var forbiddenTickets = await client.GetAsync("/api/v1/preparation-tickets");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenTickets.StatusCode);

        var courier = await LoginAsync(client, "DeliveryStaff");
        client.DefaultRequestHeaders.Authorization = new("Bearer", courier.AccessToken);
        var assignments = await client.GetFromJsonAsync<PageResponse<CourierAssignmentResponse>>("/api/v1/courier/assignments");
        Assert.Empty(assignments!.Items);
        using var invalidCourierStatus = await client.GetAsync("/api/v1/courier/assignments?status=unassigned");
        Assert.Equal(HttpStatusCode.BadRequest, invalidCourierStatus.StatusCode);
        using var forbiddenDiscounts = await client.GetAsync("/api/v1/discounts/pending");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDiscounts.StatusCode);
    }

    [PostgresFact]
    public async Task CourierAssignment_EnforcesOwnershipAndSingleDeliveryTransition()
    {
        await using (var db = Database()) await AddUserAsync(db, "othercourier", ["DeliveryStaff"]);
        using var manager = host!.CreateClient();
        var managerLogin = await LoginAsync(manager, "Manager");
        manager.DefaultRequestHeaders.Authorization = new("Bearer", managerLogin.AccessToken);
        using var categoryCreated = await manager.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("Delivery drinks"));
        var category = (await categoryCreated.Content.ReadFromJsonAsync<CategoryResponse>())!;
        using var itemCreated = await manager.PostAsJsonAsync("/api/v1/menu-items",
            new MenuItemRequest(category.Id, "Mocha", null, [new SizeVariantRequest("Regular", 5m)]));
        var item = (await itemCreated.Content.ReadFromJsonAsync<MenuItemResponse>())!;
        var orderId = Guid.NewGuid();
        using var draft = await manager.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest(orderId, "delivery", null,
            new OrderCustomerRequest(RecipientName: "Customer", ContactNumber: "1234567890", AddressLine: "123 Main St"),
            [new OrderLineRequest(item.Id, Assert.Single(item.SizeVariants).Id, 1, [])]));
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        using var confirmed = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/confirm", new ConfirmOrderRequest(5m));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        using var preparing = await manager.PostAsync($"/api/v1/orders/{orderId}/preparing", null);
        Assert.Equal(HttpStatusCode.NoContent, preparing.StatusCode);
        using var ready = await manager.PostAsync($"/api/v1/orders/{orderId}/ready", null);
        Assert.Equal(HttpStatusCode.NoContent, ready.StatusCode);
        var courierLogin = await LoginAsync(manager, "DeliveryStaff");
        using var assigned = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/assign-delivery",
            new AssignDeliveryRequest(courierLogin.User.Id));
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        using var repeatedAssignment = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/assign-delivery",
            new AssignDeliveryRequest(courierLogin.User.Id));
        Assert.Equal(HttpStatusCode.Conflict, repeatedAssignment.StatusCode);

        using var courier = host.CreateClient();
        courier.DefaultRequestHeaders.Authorization = new("Bearer", courierLogin.AccessToken);
        var assignments = await courier.GetFromJsonAsync<PageResponse<CourierAssignmentResponse>>("/api/v1/courier/assignments");
        Assert.Equal(orderId, Assert.Single(assignments!.Items).OrderId);
        var assignedOnly = await courier.GetFromJsonAsync<PageResponse<CourierAssignmentResponse>>(
            "/api/v1/courier/assignments?status=assigned&pageSize=1");
        Assert.Equal(1, assignedOnly!.TotalCount);
        Assert.Equal(orderId, Assert.Single(assignedOnly.Items).OrderId);
        var laterPage = await courier.GetFromJsonAsync<PageResponse<CourierAssignmentResponse>>(
            "/api/v1/courier/assignments?page=2&pageSize=1");
        Assert.Empty(laterPage!.Items);
        var detail = await courier.GetFromJsonAsync<CourierAssignmentDetailResponse>($"/api/v1/courier/assignments/{orderId}");
        Assert.Equal("123 Main St", detail!.AddressLine);
        using var other = host.CreateClient();
        var otherLogin = await LoginAsync(other, "othercourier");
        other.DefaultRequestHeaders.Authorization = new("Bearer", otherLogin.AccessToken);
        Assert.Empty((await other.GetFromJsonAsync<PageResponse<CourierAssignmentResponse>>(
            "/api/v1/courier/assignments"))!.Items);
        using var hidden = await other.GetAsync($"/api/v1/courier/assignments/{orderId}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var denied = await other.PostAsync($"/api/v1/courier/assignments/{orderId}/start", null);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var started = await courier.PostAsync($"/api/v1/courier/assignments/{orderId}/start", null);
        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);
        using var retry = await courier.PostAsync($"/api/v1/courier/assignments/{orderId}/start", null);
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        using var invalidFailure = await courier.PostAsJsonAsync($"/api/v1/courier/assignments/{orderId}/fail",
            new FailDeliveryRequest(" "));
        Assert.Equal(HttpStatusCode.BadRequest, invalidFailure.StatusCode);
        using var completed = await courier.PostAsync($"/api/v1/courier/assignments/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        using var repeatedCompletion = await courier.PostAsync($"/api/v1/courier/assignments/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.Conflict, repeatedCompletion.StatusCode);
        Assert.Empty((await courier.GetFromJsonAsync<PageResponse<CourierAssignmentResponse>>("/api/v1/courier/assignments"))!.Items);
        var completedAssignments = await courier.GetFromJsonAsync<PageResponse<CourierAssignmentResponse>>(
            "/api/v1/courier/assignments?status=delivered,failed");
        Assert.Equal(orderId, Assert.Single(completedAssignments!.Items).OrderId);
        Assert.Empty((await other.GetFromJsonAsync<PageResponse<CourierAssignmentResponse>>(
            "/api/v1/courier/assignments?status=delivered,failed"))!.Items);
        using var expiredDetail = await courier.GetAsync($"/api/v1/courier/assignments/{orderId}");
        Assert.Equal(HttpStatusCode.NotFound, expiredDetail.StatusCode);
        var failedId = Guid.NewGuid();
        using var failedDraft = await manager.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest(failedId, "delivery", null,
            new OrderCustomerRequest(RecipientName: "Customer", ContactNumber: "1234567890", AddressLine: "456 Main St"),
            [new OrderLineRequest(item.Id, Assert.Single(item.SizeVariants).Id, 1, [])]));
        Assert.Equal(HttpStatusCode.Created, failedDraft.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsJsonAsync($"/api/v1/orders/{failedId}/confirm", new ConfirmOrderRequest(5m))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.PostAsync($"/api/v1/orders/{failedId}/preparing", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.PostAsync($"/api/v1/orders/{failedId}/ready", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.PostAsJsonAsync($"/api/v1/orders/{failedId}/assign-delivery", new AssignDeliveryRequest(courierLogin.User.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await courier.PostAsync($"/api/v1/courier/assignments/{failedId}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await courier.PostAsJsonAsync($"/api/v1/courier/assignments/{failedId}/fail", new FailDeliveryRequest(" "))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await courier.PostAsJsonAsync($"/api/v1/courier/assignments/{failedId}/fail", new FailDeliveryRequest("Customer unavailable"))).StatusCode);
        var failedAssignments = await courier.GetFromJsonAsync<PageResponse<CourierAssignmentResponse>>(
            "/api/v1/courier/assignments?status=failed");
        Assert.Equal(failedId, Assert.Single(failedAssignments!.Items).OrderId);
        Assert.Empty((await other.GetFromJsonAsync<PageResponse<CourierAssignmentResponse>>("/api/v1/courier/assignments?status=failed"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await courier.GetAsync($"/api/v1/courier/assignments/{failedId}")).StatusCode);
        await using var finalDb = Database();
        var history = await finalDb.DeliveryStatusHistories.ToArrayAsync();
        Assert.Equal(6, history.Length);
        Assert.All(history.Where(change => change.ToStatus == "assigned"), change => Assert.Equal(new EntityId(managerLogin.User.Id), change.ActorId));
        Assert.All(history.Where(change => change.ToStatus != "assigned"),
            change => Assert.Equal(new EntityId(courierLogin.User.Id), change.ActorId));
        Assert.Equal("Customer unavailable", (await finalDb.Deliveries.SingleAsync(delivery => delivery.OrderId == new EntityId(failedId))).FailureReason);
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
        Assert.Empty(Assert.Single(draft.Lines).AddonDetails!);
        Assert.NotNull(await cashier.GetFromJsonAsync<PosTableResponse[]>("/api/v1/pos/tables"));
        using var retriedDraft = await cashier.PostAsJsonAsync("/api/v1/orders", draftRequest);
        Assert.Equal(HttpStatusCode.Created, retriedDraft.StatusCode);
        await using (var db = Database()) Assert.Single(await db.Orders.Where(order => order.Id == new EntityId(orderId)).ToArrayAsync());
        var discountId = Guid.NewGuid();
        using var requested = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/discounts",
            new DiscountRequest(discountId, "fixed", 1m, "Customer apology"));
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var discountedDraft = await cashier.GetFromJsonAsync<OrderResponse>($"/api/v1/orders/{orderId}");
        Assert.Equal("Customer apology", Assert.Single(discountedDraft!.Discounts).Reason);
        var pending = await manager.GetFromJsonAsync<PageResponse<PendingDiscountResponse>>("/api/v1/discounts/pending");
        Assert.Equal("Customer apology", Assert.Single(pending!.Items).Reason);
        Assert.Equal(1, pending.TotalCount);
        using var rejected = await manager.PostAsync($"/api/v1/discounts/{discountId}/reject", null);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Empty((await manager.GetFromJsonAsync<PageResponse<PendingDiscountResponse>>(
            "/api/v1/discounts/pending"))!.Items);
        using var repeatedDecision = await manager.PostAsync($"/api/v1/discounts/{discountId}/reject", null);
        Assert.Equal(HttpStatusCode.Conflict, repeatedDecision.StatusCode);
        using var wrongTotal = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/confirm", new ConfirmOrderRequest(4m));
        Assert.Equal(HttpStatusCode.Conflict, wrongTotal.StatusCode);
        using var confirmed = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/confirm", new ConfirmOrderRequest(5m));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        var paymentId = Guid.NewGuid();
        var payment = new RecordPaymentRequest(paymentId, "cash", 5m);
        using var paid = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", payment);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var paymentResult = (await paid.Content.ReadFromJsonAsync<PaymentResponse>())!;
        Assert.Equal(paymentId, paymentResult.PaymentId);
        Assert.False(paymentResult.AlreadyRecorded);
        using var repeated = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", payment);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.True((await repeated.Content.ReadFromJsonAsync<PaymentResponse>())!.AlreadyRecorded);
        using var mismatch = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", payment with { Amount = 4m });
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        await using (var db = Database())
            Assert.Equal(new EntityId(cashierLogin.User.Id),
                (await db.Payments.SingleAsync(entry => entry.Id == new EntityId(paymentId))).RecordedBy);

        using var barista = host.CreateClient();
        var baristaLogin = await LoginAsync(barista, "Barista");
        barista.DefaultRequestHeaders.Authorization = new("Bearer", baristaLogin.AccessToken);
        var tickets = await barista.GetFromJsonAsync<PageResponse<PreparationTicketResponse>>("/api/v1/preparation-tickets");
        Assert.Contains(tickets!.Items, ticket => ticket.OrderId == orderId && ticket.Lines.Count == 1);
        var filteredTickets = await barista.GetFromJsonAsync<PageResponse<PreparationTicketResponse>>(
            "/api/v1/preparation-tickets?status=confirmed&pageSize=1");
        Assert.Equal(orderId, Assert.Single(filteredTickets!.Items).OrderId);
        Assert.Equal(1, filteredTickets.TotalCount);
        Assert.Empty((await barista.GetFromJsonAsync<PageResponse<PreparationTicketResponse>>(
            "/api/v1/preparation-tickets?status=preparing"))!.Items);
        var ticket = await barista.GetFromJsonAsync<PreparationTicketResponse>($"/api/v1/preparation-tickets/{orderId}");
        Assert.Equal("Latte", Assert.Single(ticket!.Lines).ItemName);
        using var preparing = await barista.PostAsync($"/api/v1/preparation-tickets/{orderId}/start", null);
        Assert.Equal(HttpStatusCode.NoContent, preparing.StatusCode);
        Assert.Equal(orderId, Assert.Single((await barista.GetFromJsonAsync<PageResponse<PreparationTicketResponse>>(
            "/api/v1/preparation-tickets?status=preparing"))!.Items).OrderId);
        using var ready = await barista.PostAsync($"/api/v1/preparation-tickets/{orderId}/ready", null);
        Assert.Equal(HttpStatusCode.NoContent, ready.StatusCode);
        using var repeatReady = await barista.PostAsync($"/api/v1/preparation-tickets/{orderId}/ready", null);
        Assert.Equal(HttpStatusCode.Conflict, repeatReady.StatusCode);
        var shared = await manager.GetFromJsonAsync<OrderResponse>($"/api/v1/orders/{orderId}");
        Assert.Equal("ready", shared!.Status);
        var serviceQueue = await barista.GetFromJsonAsync<OperationalOrderResponse[]>("/api/v1/operations/orders");
        var queueOrder = Assert.Single(serviceQueue!);
        Assert.Equal(orderId, queueOrder.Id);
        Assert.Equal(0m, queueOrder.Total);
        var serviceDetail = await barista.GetFromJsonAsync<OperationsOrderDetailResponse>(
            $"/api/v1/operations/orders/{orderId}");
        Assert.Equal("Latte", Assert.Single(serviceDetail!.Lines).ItemName);
        Assert.Empty(serviceDetail.Discounts);

        // Seed a table and verify dine-in creation, occupancy, cancellation, and release over HTTP.
        var tableId = Guid.NewGuid();
        await using (var db = Database())
        {
            db.DiningTables.Add(new DiningTable(new(tableId), "T-154", 2));
            await db.SaveChangesAsync();
        }
        Assert.Contains((await cashier.GetFromJsonAsync<PosTableResponse[]>("/api/v1/pos/tables"))!,
            table => table.Id == tableId);
        var dineInId = Guid.NewGuid();
        using var dineInDraft = await cashier.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest(dineInId,
            "dine_in", tableId, null, [new OrderLineRequest(item.Id, variant.Id, 1, [])]));
        Assert.Equal(HttpStatusCode.Created, dineInDraft.StatusCode);
        using var dineInConfirmed = await cashier.PostAsJsonAsync($"/api/v1/orders/{dineInId}/confirm",
            new ConfirmOrderRequest(5m));
        Assert.Equal(HttpStatusCode.OK, dineInConfirmed.StatusCode);
        var occupiedTable = Assert.Single((await cashier.GetFromJsonAsync<PosTableResponse[]>("/api/v1/pos/tables"))!,
            table => table.Id == tableId);
        Assert.True(occupiedTable.IsOccupied);
        Assert.Equal(dineInId, occupiedTable.ActiveOrderId);
        using var blankCancellation = await manager.PostAsJsonAsync($"/api/v1/orders/{dineInId}/cancel",
            new CancelOrderRequest(" "));
        Assert.Equal(HttpStatusCode.BadRequest, blankCancellation.StatusCode);
        using var forbiddenCancellation = await cashier.PostAsJsonAsync($"/api/v1/orders/{dineInId}/cancel",
            new CancelOrderRequest("Customer changed plans"));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCancellation.StatusCode);
        using var cancelled = await manager.PostAsJsonAsync($"/api/v1/orders/{dineInId}/cancel",
            new CancelOrderRequest("Customer changed plans"));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("cancelled", (await manager.GetFromJsonAsync<OrderResponse>($"/api/v1/orders/{dineInId}"))!.Status);
        using var repeatedCancellation = await manager.PostAsJsonAsync($"/api/v1/orders/{dineInId}/cancel",
            new CancelOrderRequest("Retry"));
        Assert.Equal(HttpStatusCode.Conflict, repeatedCancellation.StatusCode);
        var releasedTable = Assert.Single((await cashier.GetFromJsonAsync<PosTableResponse[]>("/api/v1/pos/tables"))!,
            table => table.Id == tableId);
        Assert.False(releasedTable.IsOccupied);
        Assert.Null(releasedTable.ActiveOrderId);

        // Compare seeded API reports with the application query handlers over the same persisted payments.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var range = $"from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&timeZone=UTC";
        using var reportScope = host.Services.CreateScope();
        var revenueBaseline = (await reportScope.ServiceProvider.GetRequiredService<RevenueReportQueryHandler>()
            .GetRevenueAsync(new(today, today, TimeZoneInfo.Utc))).Value;
        var bestSellerBaseline = (await reportScope.ServiceProvider.GetRequiredService<BestSellerReportQueryHandler>()
            .GetBestSellersAsync(new(today, today, TimeZoneInfo.Utc))).Value;
        var channelBaseline = (await reportScope.ServiceProvider.GetRequiredService<OrderChannelReportQueryHandler>()
            .GetChannelsAsync(new(today, today, TimeZoneInfo.Utc))).Value;
        var seededRevenue = await manager.GetFromJsonAsync<RevenueReportResponse>($"/api/v1/reports/revenue?{range}");
        var seededBestSellers = await manager.GetFromJsonAsync<BestSellerReportResponse>($"/api/v1/reports/best-sellers?{range}");
        var seededChannels = await manager.GetFromJsonAsync<ChannelReportResponse>($"/api/v1/reports/channels?{range}");
        Assert.Equal(revenueBaseline.Revenue, seededRevenue!.Revenue);
        Assert.Equal(revenueBaseline.OrderCount, seededRevenue.OrderCount);
        Assert.Equal(bestSellerBaseline.Items.Select(row => (row.ItemName, row.Quantity, row.Revenue)),
            seededBestSellers!.Items.Select(row => (row.ItemName, row.Quantity, row.Revenue)));
        Assert.Equal(channelBaseline.ByChannel.Select(row => (row.Channel, row.Revenue, row.OrderCount)),
            seededChannels!.ByChannel.Select(row => (row.Channel, row.Revenue, row.OrderCount)));
    }

    [PostgresFact]
    public async Task Preparation_RejectsUnauthorizedAndStaleTransitionsWithoutChangingNewerState()
    {
        using var manager = host!.CreateClient();
        var managerLogin = await LoginAsync(manager, "Manager");
        manager.DefaultRequestHeaders.Authorization = new("Bearer", managerLogin.AccessToken);
        using var categoryCreated = await manager.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("Preparation drinks"));
        var category = (await categoryCreated.Content.ReadFromJsonAsync<CategoryResponse>())!;
        using var itemCreated = await manager.PostAsJsonAsync("/api/v1/menu-items",
            new MenuItemRequest(category.Id, "Latte", null, [new SizeVariantRequest("Large", 5m)]));
        var item = (await itemCreated.Content.ReadFromJsonAsync<MenuItemResponse>())!;

        async Task<Guid> ConfirmOrder()
        {
            var id = Guid.NewGuid();
            using var created = await manager.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest(id, "takeaway", null, null,
                [new OrderLineRequest(item.Id, Assert.Single(item.SizeVariants).Id, 1, [])]));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var confirmed = await manager.PostAsJsonAsync($"/api/v1/orders/{id}/confirm", new ConfirmOrderRequest(5m));
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            return id;
        }

        var orderId = await ConfirmOrder();
        using var unauthorized = host.CreateClient();
        foreach (var path in new[] { "/api/v1/preparation-tickets", $"/api/v1/preparation-tickets/{orderId}" })
        {
            using var response = await unauthorized.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        var delivery = await LoginAsync(unauthorized, "DeliveryStaff");
        unauthorized.DefaultRequestHeaders.Authorization = new("Bearer", delivery.AccessToken);
        foreach (var path in new[] { "/api/v1/preparation-tickets", $"/api/v1/preparation-tickets/{orderId}" })
        {
            using var response = await unauthorized.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        foreach (var action in new[] { "start", "ready" })
        {
            using var response = await unauthorized.PostAsync($"/api/v1/preparation-tickets/{orderId}/{action}", null);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using var barista = host.CreateClient();
        var login = await LoginAsync(barista, "Barista");
        barista.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        using var startedElsewhere = await manager.PostAsync($"/api/v1/preparation-tickets/{orderId}/start", null);
        Assert.Equal(HttpStatusCode.NoContent, startedElsewhere.StatusCode);
        using var staleStart = await barista.PostAsync($"/api/v1/preparation-tickets/{orderId}/start", null);
        Assert.Equal(HttpStatusCode.Conflict, staleStart.StatusCode);
        var current = await barista.GetFromJsonAsync<PreparationTicketResponse>($"/api/v1/preparation-tickets/{orderId}");
        Assert.Equal("preparing", current!.Status);
        using var ready = await barista.PostAsync($"/api/v1/preparation-tickets/{orderId}/ready", null);
        Assert.Equal(HttpStatusCode.NoContent, ready.StatusCode);
        using var repeated = await barista.PostAsync($"/api/v1/preparation-tickets/{orderId}/ready", null);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);

        var cancelledId = await ConfirmOrder();
        using var cancelled = await manager.PostAsJsonAsync($"/api/v1/orders/{cancelledId}/cancel", new CancelOrderRequest("Changed elsewhere"));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        foreach (var action in new[] { "start", "ready" })
        {
            using var response = await barista.PostAsync($"/api/v1/preparation-tickets/{cancelledId}/{action}", null);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        using var removed = await barista.GetAsync($"/api/v1/preparation-tickets/{cancelledId}");
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);

        await using var db = Database();
        Assert.Equal("ready", (await db.Orders.SingleAsync(order => order.Id == new EntityId(orderId))).Status);
        Assert.Equal("cancelled", (await db.Orders.SingleAsync(order => order.Id == new EntityId(cancelledId))).Status);
        var changes = await db.OrderStatusHistories.Where(change => change.OrderId == new EntityId(orderId)).ToArrayAsync();
        var preparing = Assert.Single(changes, change => change.ToStatus == "preparing");
        var readyChange = Assert.Single(changes, change => change.ToStatus == "ready");
        Assert.Equal(new EntityId(managerLogin.User.Id), preparing.ChangedBy);
        Assert.Equal(new EntityId(login.User.Id), readyChange.ChangedBy);
        Assert.True(readyChange.ChangedAt.Value >= preparing.ChangedAt.Value);
        Assert.Equal(3, changes.Length);
        Assert.Single(await db.OrderStatusHistories.Where(change => change.OrderId == new EntityId(cancelledId) && change.ToStatus == "cancelled").ToArrayAsync());
    }

    [PostgresFact]
    public async Task CustomerRoutes_ManageProfilesAndAddressesWithPermissionAndPersistence()
    {
        using var manager = host!.CreateClient();
        using var anonymous = await manager.GetAsync("/api/v1/customers");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var managerLogin = await LoginAsync(manager, "Manager");
        manager.DefaultRequestHeaders.Authorization = new("Bearer", managerLogin.AccessToken);
        var customerRequest = new CustomerRequest("Ada Lovelace", Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString(), "ada@example.test");
        using var created = await manager.PostAsJsonAsync("/api/v1/customers", customerRequest);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var customer = (await created.Content.ReadFromJsonAsync<CustomerResponse>())!;

        using var duplicate = await manager.PostAsJsonAsync("/api/v1/customers", customerRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var addressCreated = await manager.PostAsJsonAsync($"/api/v1/customers/{customer.Id}/addresses",
            new CustomerAddressRequest("12 Analytical Engine Way", "Home", true));
        Assert.Equal(HttpStatusCode.Created, addressCreated.StatusCode);
        var address = (await addressCreated.Content.ReadFromJsonAsync<CustomerAddressResponse>())!;

        var detail = await manager.GetFromJsonAsync<CustomerDetailResponse>($"/api/v1/customers/{customer.Id}");
        Assert.Equal(customer.Id, detail!.Id);
        Assert.Equal("Ada Lovelace", detail.FullName);
        Assert.Equal("12 Analytical Engine Way", Assert.Single(detail.Addresses).AddressLine);
        Assert.Equal(customer.Id, Assert.Single((await manager.GetFromJsonAsync<CustomerResponse[]>(
            "/api/v1/customers?search=Ada"))!.Select(item => item.Id)));
        using var loyaltyHistory = await manager.GetAsync(
            $"/api/v1/customers/{customer.Id}/loyalty-transactions?offset=0&limit=10");
        Assert.Equal(HttpStatusCode.OK, loyaltyHistory.StatusCode);

        using var updated = await manager.PatchAsJsonAsync($"/api/v1/customers/{customer.Id}",
            new CustomerRequest("Ada Byron", customerRequest.Phone, customerRequest.Email));
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        using var inactiveAddress = await manager.PostAsync(
            $"/api/v1/customers/{customer.Id}/addresses/{address.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, inactiveAddress.StatusCode);
        using var deactivated = await manager.PostAsync($"/api/v1/customers/{customer.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        Assert.Empty(await manager.GetFromJsonAsync<CustomerResponse[]>("/api/v1/customers?search=Ada") ?? []);
        Assert.Single(await manager.GetFromJsonAsync<CustomerResponse[]>("/api/v1/customers?search=Ada&isActive=false") ?? []);
        using var otherCustomer = await manager.PostAsJsonAsync("/api/v1/customers",
            new CustomerRequest("Ada King", Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString(), "ada-king@example.test"));
        Assert.Equal(HttpStatusCode.Created, otherCustomer.StatusCode);
        var allCustomers = (await manager.GetFromJsonAsync<CustomerResponse[]>("/api/v1/customers?search=Ada&includeInactive=true"))!;
        Assert.Equal(2, allCustomers.Length);
        Assert.Contains(allCustomers, item => item.Id == customer.Id && !item.IsActive);
        Assert.Contains(allCustomers, item => item.FullName == "Ada King" && item.IsActive);

        foreach (var role in new[] { "Barista", "InventoryStaff", "DeliveryStaff" })
        {
            using var unauthorized = host.CreateClient();
            var login = await LoginAsync(unauthorized, role);
            unauthorized.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
            foreach (var path in new[]
            {
                "/api/v1/customers",
                $"/api/v1/customers/{customer.Id}",
                $"/api/v1/customers/{customer.Id}/addresses",
                $"/api/v1/customers/{customer.Id}/loyalty-transactions"
            })
            {
                using var forbiddenRead = await unauthorized.GetAsync(path);
                Assert.Equal(HttpStatusCode.Forbidden, forbiddenRead.StatusCode);
                var errorBody = await forbiddenRead.Content.ReadAsStringAsync();
                Assert.DoesNotContain("Ada Byron", errorBody, StringComparison.Ordinal);
                Assert.DoesNotContain("ada@example.test", errorBody, StringComparison.Ordinal);
                Assert.DoesNotContain(customerRequest.Phone, errorBody, StringComparison.Ordinal);
                Assert.DoesNotContain("12 Analytical Engine Way", errorBody, StringComparison.Ordinal);
            }
        }
        using var cashier = host.CreateClient();
        var cashierLogin = await LoginAsync(cashier, "Cashier");
        cashier.DefaultRequestHeaders.Authorization = new("Bearer", cashierLogin.AccessToken);
        using var cashierList = await cashier.GetAsync($"/api/v1/customers?search={Uri.EscapeDataString("Ada Byron")}&isActive=false");
        Assert.Equal(HttpStatusCode.OK, cashierList.StatusCode);
        var cashierRows = await cashierList.Content.ReadFromJsonAsync<CustomerResponse[]>();
        Assert.Contains(cashierRows!, row => row.Id == customer.Id);
        using var cashierDetail = await cashier.GetAsync($"/api/v1/customers/{customer.Id}");
        Assert.Equal(HttpStatusCode.OK, cashierDetail.StatusCode);
        await using var db = Database();
        Assert.False((await db.Customers.SingleAsync(item => item.Id == new EntityId(customer.Id))).IsActive);
    }

    [PostgresFact]
    public async Task OptionalCustomerOrders_ChangeLoyaltyBalanceThroughEarnAndRedemption()
    {
        using var manager = host!.CreateClient();
        var adminLogin = await LoginAsync(manager, "Admin");
        manager.DefaultRequestHeaders.Authorization = new("Bearer", adminLogin.AccessToken);
        using var settingsUpdated = await manager.PatchAsJsonAsync("/api/v1/loyalty-settings",
            new LoyaltySettingsRequest(1m, 0.1m, 100m));
        Assert.Equal(HttpStatusCode.OK, settingsUpdated.StatusCode);

        using var categoryCreated = await manager.PostAsJsonAsync("/api/v1/categories",
            new CreateCategoryRequest("Loyalty drinks"));
        var category = (await categoryCreated.Content.ReadFromJsonAsync<CategoryResponse>())!;
        using var itemCreated = await manager.PostAsJsonAsync("/api/v1/menu-items",
            new MenuItemRequest(category.Id, "Loyalty latte", null, [new SizeVariantRequest("Regular", 5m)]));
        var item = (await itemCreated.Content.ReadFromJsonAsync<MenuItemResponse>())!;
        var variant = Assert.Single(item.SizeVariants);

        var walkInId = Guid.NewGuid();
        using var walkIn = await manager.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest(walkInId,
            "takeaway", null, null, [new OrderLineRequest(item.Id, variant.Id, 1, [])]));
        Assert.Equal(HttpStatusCode.Created, walkIn.StatusCode);
        Assert.Null((await walkIn.Content.ReadFromJsonAsync<OrderResponse>())!.CustomerId);

        using var customerCreated = await manager.PostAsJsonAsync("/api/v1/customers",
            new CustomerUpsertRequest("Grace Hopper", Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString()));
        Assert.Equal(HttpStatusCode.Created, customerCreated.StatusCode);
        var customer = (await customerCreated.Content.ReadFromJsonAsync<CustomerDetailResponse>())!;
        var earningOrderId = Guid.NewGuid();
        using var earningDraft = await manager.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest(earningOrderId,
            "takeaway", null, new OrderCustomerRequest(customer.Id),
            [new OrderLineRequest(item.Id, variant.Id, 1, [])]));
        Assert.Equal(HttpStatusCode.Created, earningDraft.StatusCode);
        using var earningConfirmed = await manager.PostAsJsonAsync($"/api/v1/orders/{earningOrderId}/confirm",
            new ConfirmOrderRequest(5m));
        Assert.Equal(HttpStatusCode.OK, earningConfirmed.StatusCode);
        using var earningPayment = await manager.PostAsJsonAsync($"/api/v1/orders/{earningOrderId}/payments",
            new RecordPaymentRequest(Guid.NewGuid(), "cash", 5m));
        Assert.Equal(HttpStatusCode.OK, earningPayment.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await manager.PostAsync($"/api/v1/orders/{earningOrderId}/preparing", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await manager.PostAsync($"/api/v1/orders/{earningOrderId}/ready", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await manager.PostAsync($"/api/v1/orders/{earningOrderId}/complete", null)).StatusCode);

        var earnedCustomer = await manager.GetFromJsonAsync<CustomerDetailResponse>($"/api/v1/customers/{customer.Id}");
        Assert.Equal(5, earnedCustomer!.LoyaltyPointsBalance);
        var history = await manager.GetFromJsonAsync<LoyaltyHistoryResponse>(
            $"/api/v1/customers/{customer.Id}/loyalty-transactions?offset=0&limit=10");
        var earned = Assert.Single(history!.Items);
        Assert.Equal("earn", earned.Type);
        Assert.Equal(5, earned.Points);
        Assert.Equal(earningOrderId, earned.OrderId);

        var redemptionOrderId = Guid.NewGuid();
        using var redemptionDraft = await manager.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest(redemptionOrderId,
            "takeaway", null, new OrderCustomerRequest(customer.Id),
            [new OrderLineRequest(item.Id, variant.Id, 1, [])]));
        Assert.Equal(HttpStatusCode.Created, redemptionDraft.StatusCode);
        using var tooManyPoints = await manager.PostAsJsonAsync($"/api/v1/orders/{redemptionOrderId}/loyalty/redeem",
            new RedeemLoyaltyRequest(6));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooManyPoints.StatusCode);
        using var redeemed = await manager.PostAsJsonAsync($"/api/v1/orders/{redemptionOrderId}/loyalty/redeem",
            new RedeemLoyaltyRequest(3));
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);
        var redemption = (await redeemed.Content.ReadFromJsonAsync<LoyaltyOperationResponse>())!;
        Assert.Equal(0.3m, redemption.DiscountAmount);
        Assert.False(redemption.AlreadyApplied);
        using var retry = await manager.PostAsJsonAsync($"/api/v1/orders/{redemptionOrderId}/loyalty/redeem",
            new RedeemLoyaltyRequest(3));
        Assert.True((await retry.Content.ReadFromJsonAsync<LoyaltyOperationResponse>())!.AlreadyApplied);
        var redeemedCustomer = await manager.GetFromJsonAsync<CustomerDetailResponse>($"/api/v1/customers/{customer.Id}");
        Assert.Equal(2, redeemedCustomer!.LoyaltyPointsBalance);
        await using var db = Database();
        Assert.Equal(2, await db.LoyaltyTransactions.CountAsync(transaction => transaction.CustomerId == new EntityId(customer.Id)));
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
        var unchangedAfterValidation = await client.GetFromJsonAsync<IngredientBalanceResponse>(
            $"/api/v1/ingredients/{ingredient.IngredientId}");
        Assert.Equal(2m, unchangedAfterValidation!.AvailableQuantity);
        var movementsAfterValidation = await client.GetFromJsonAsync<StockMovementResponse[]>(
            $"/api/v1/ingredients/{ingredient.IngredientId}/movements");
        Assert.Single(movementsAfterValidation!);

        var lowStock = await client.GetFromJsonAsync<PageResponse<IngredientBalanceResponse>>("/api/v1/ingredients/low-stock");
        Assert.Contains(lowStock!.Items, balance => balance.IngredientId == ingredient.IngredientId);
        var searched = await client.GetFromJsonAsync<PageResponse<IngredientBalanceResponse>>(
            "/api/v1/ingredients/low-stock?search=Test&pageSize=1");
        Assert.Equal(ingredient.IngredientId, Assert.Single(searched!.Items).IngredientId);
        await using (var db = Database())
            Assert.Single(await db.StockMovements.Where(movement => movement.IngredientId == new EntityId(ingredient.IngredientId)).ToArrayAsync());

        var cashier = await LoginAsync(client, "Cashier");
        client.DefaultRequestHeaders.Authorization = new("Bearer", cashier.AccessToken);
        using var forbidden = await client.PostAsJsonAsync($"/api/v1/ingredients/{ingredient.IngredientId}/adjustments",
            new StockAdjustmentRequest(Guid.NewGuid(), 1m, "Unauthorized"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var inventoryAfterForbidden = await LoginAsync(client, "InventoryStaff");
        client.DefaultRequestHeaders.Authorization = new("Bearer", inventoryAfterForbidden.AccessToken);
        var unchangedAfterForbidden = await client.GetFromJsonAsync<IngredientBalanceResponse>(
            $"/api/v1/ingredients/{ingredient.IngredientId}");
        Assert.Equal(2m, unchangedAfterForbidden!.AvailableQuantity);
        var movementsAfterForbidden = await client.GetFromJsonAsync<StockMovementResponse[]>(
            $"/api/v1/ingredients/{ingredient.IngredientId}/movements");
        Assert.Single(movementsAfterForbidden!);
    }

    [PostgresFact]
    public async Task ConcurrentStockAdjustmentRetries_CommitOnceAndRejectCrossIngredientReuse()
    {
        using var client = host!.CreateClient();
        var inventory = await LoginAsync(client, "InventoryStaff");
        client.DefaultRequestHeaders.Authorization = new("Bearer", inventory.AccessToken);
        var ingredients = new List<IngredientBalanceResponse>();
        foreach (var name in new[] { "Concurrent beans A", "Concurrent beans B" })
        {
            using var created = await client.PostAsJsonAsync("/api/v1/ingredients",
                new CreateIngredientRequest(name, "g", 0m, 0m));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            ingredients.Add((await created.Content.ReadFromJsonAsync<IngredientBalanceResponse>())!);
        }

        var sameRequest = new StockAdjustmentRequest(Guid.NewGuid(), 2m, "Counted stock");
        var sameResponses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            client.PostAsJsonAsync($"/api/v1/ingredients/{ingredients[0].IngredientId}/adjustments", sameRequest)));
        try { Assert.All(sameResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode)); }
        finally { foreach (var response in sameResponses) response.Dispose(); }

        var crossRequest = new StockAdjustmentRequest(Guid.NewGuid(), 1m, "Counted stock");
        var crossResponses = await Task.WhenAll(ingredients.Select(ingredient =>
            client.PostAsJsonAsync($"/api/v1/ingredients/{ingredient.IngredientId}/adjustments", crossRequest)));
        try
        {
            Assert.Single(crossResponses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(crossResponses, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in crossResponses) response.Dispose(); }

        await using var db = Database();
        Assert.Equal(2, await db.StockMovements.CountAsync());
        Assert.Equal(3m, (await db.Ingredients.Where(ingredient =>
            ingredient.Id == new EntityId(ingredients[0].IngredientId) || ingredient.Id == new EntityId(ingredients[1].IngredientId))
            .ToArrayAsync()).Sum(ingredient => ingredient.AvailableQuantity));
        Assert.All(await db.StockMovements.ToArrayAsync(), movement => Assert.Equal(new EntityId(inventory.User.Id), movement.ActorId));
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

    [PostgresFact]
    public async Task MobileCashier182_ConcurrentRetriesPersistOnePaymentAndRefreshSharedBalance()
    {
        using var manager = host!.CreateClient();
        var managerLogin = await LoginAsync(manager, "Manager");
        manager.DefaultRequestHeaders.Authorization = new("Bearer", managerLogin.AccessToken);
        using var categoryCreated = await manager.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("Counter verification"));
        var category = (await categoryCreated.Content.ReadFromJsonAsync<CategoryResponse>())!;
        using var itemCreated = await manager.PostAsJsonAsync("/api/v1/menu-items",
            new MenuItemRequest(category.Id, "Counter latte", null, [new SizeVariantRequest("Large", 10m)]));
        var item = (await itemCreated.Content.ReadFromJsonAsync<MenuItemResponse>())!;
        var orderId = Guid.NewGuid();
        using var draft = await manager.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest(orderId, "takeaway", null, null,
            [new OrderLineRequest(item.Id, Assert.Single(item.SizeVariants).Id, 1, [])]));
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        using var confirmed = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/confirm", new ConfirmOrderRequest(10m));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        using var cashier = host.CreateClient();
        var cashierLogin = await LoginAsync(cashier, "Cashier");
        cashier.DefaultRequestHeaders.Authorization = new("Bearer", cashierLogin.AccessToken);
        var paymentIds = new List<Guid>();
        foreach (var method in new[] { "cash", "card", "e_wallet" })
        {
            var payment = new RecordPaymentRequest(Guid.NewGuid(), method, 2m, Reference: method == "cash" ? null : $"receipt-{method}");
            paymentIds.Add(payment.PaymentId);
            var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
                cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", payment)));
            try
            {
                Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
                var results = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<PaymentResponse>()));
                Assert.Single(results, result => !result!.AlreadyRecorded);
                Assert.Equal(2, results.Count(result => result!.AlreadyRecorded));
            }
            finally { foreach (var response in responses) response.Dispose(); }
            using var mismatch = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", payment with { Amount = 1m });
            Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        }
        var shared = Assert.Single((await manager.GetFromJsonAsync<OperationalOrderResponse[]>("/api/v1/orders"))!, row => row.Id == orderId);
        Assert.Equal(6m, shared.PaidAmount);
        Assert.Equal(4m, shared.Total - shared.PaidAmount);
        using var cancelled = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/cancel", new CancelOrderRequest("Counter verification complete"));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        using var rejected = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new RecordPaymentRequest(Guid.NewGuid(), "cash", 4m));
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using var barista = host.CreateClient();
        var baristaLogin = await LoginAsync(barista, "Barista");
        barista.DefaultRequestHeaders.Authorization = new("Bearer", baristaLogin.AccessToken);
        using var denied = await barista.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new RecordPaymentRequest(Guid.NewGuid(), "cash", 1m));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await using var db = Database();
        var payments = await db.Payments.Where(payment => payment.OrderId == new EntityId(orderId)).ToArrayAsync();
        Assert.Equal(3, payments.Length);
        Assert.All(payments, payment =>
        {
            Assert.Contains(payment.Id.Value, paymentIds);
            Assert.Equal(new EntityId(cashierLogin.User.Id), payment.RecordedBy);
        });
    }

    [PostgresFact]
    public async Task MobileManager182_RejectsIneligibleAssignmentsAndAuditsCancellationWithoutDuplicateEffects()
    {
        using var manager = host!.CreateClient();
        var managerLogin = await LoginAsync(manager, "Manager");
        manager.DefaultRequestHeaders.Authorization = new("Bearer", managerLogin.AccessToken);
        using var categoryCreated = await manager.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("Manager verification"));
        var category = (await categoryCreated.Content.ReadFromJsonAsync<CategoryResponse>())!;
        using var itemCreated = await manager.PostAsJsonAsync("/api/v1/menu-items",
            new MenuItemRequest(category.Id, "Delivery latte", null, [new SizeVariantRequest("Large", 5m)]));
        var item = (await itemCreated.Content.ReadFromJsonAsync<MenuItemResponse>())!;
        var orderId = Guid.NewGuid();
        using var draft = await manager.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest(orderId, "delivery", null,
            new OrderCustomerRequest(RecipientName: "Synthetic customer", ContactNumber: "1234567890", AddressLine: "Test address"),
            [new OrderLineRequest(item.Id, Assert.Single(item.SizeVariants).Id, 1, [])]));
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        using var confirmed = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/confirm", new ConfirmOrderRequest(5m));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var options = (await manager.GetFromJsonAsync<DeliveryStaffResponse[]>("/api/v1/delivery-staff/options"))!;
        var courier = Assert.Single(options);
        Assert.DoesNotContain(options, staff => staff.Id == managerLogin.User.Id);
        using var notReady = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/assign-delivery", new AssignDeliveryRequest(courier.Id));
        Assert.Equal(HttpStatusCode.Conflict, notReady.StatusCode);
        using var preparing = await manager.PostAsync($"/api/v1/orders/{orderId}/preparing", null);
        Assert.Equal(HttpStatusCode.NoContent, preparing.StatusCode);
        using var ready = await manager.PostAsync($"/api/v1/orders/{orderId}/ready", null);
        Assert.Equal(HttpStatusCode.NoContent, ready.StatusCode);
        using var ineligible = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/assign-delivery", new AssignDeliveryRequest(managerLogin.User.Id));
        Assert.Equal(HttpStatusCode.BadRequest, ineligible.StatusCode);
        using var cashier = host.CreateClient();
        var cashierLogin = await LoginAsync(cashier, "Cashier");
        cashier.DefaultRequestHeaders.Authorization = new("Bearer", cashierLogin.AccessToken);
        using var forbidden = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/assign-delivery", new AssignDeliveryRequest(courier.Id));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        await using (var db = Database())
        {
            var delivery = await db.Deliveries.SingleAsync(entry => entry.OrderId == new EntityId(orderId));
            Assert.Equal("unassigned", delivery.Status);
            Assert.Null(delivery.AssignedStaffId);
            Assert.Empty(await db.DeliveryStatusHistories.ToArrayAsync());
        }
        using var assigned = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/assign-delivery", new AssignDeliveryRequest(courier.Id));
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        using var repeatedAssignment = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/assign-delivery", new AssignDeliveryRequest(courier.Id));
        Assert.Equal(HttpStatusCode.Conflict, repeatedAssignment.StatusCode);
        using var blank = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/cancel", new CancelOrderRequest(" "));
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        using var forbiddenCancel = await cashier.PostAsJsonAsync($"/api/v1/orders/{orderId}/cancel", new CancelOrderRequest("Unauthorized"));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCancel.StatusCode);
        Assert.Equal("ready", (await manager.GetFromJsonAsync<OrderResponse>($"/api/v1/orders/{orderId}"))!.Status);
        using var cancelled = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/cancel", new CancelOrderRequest(" Customer changed plans "));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        using var repeatedCancellation = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/cancel", new CancelOrderRequest("Retry"));
        Assert.Equal(HttpStatusCode.Conflict, repeatedCancellation.StatusCode);
        await using var finalDb = Database();
        var order = await finalDb.Orders.SingleAsync(entry => entry.Id == new EntityId(orderId));
        Assert.Equal("cancelled", order.Status);
        Assert.Equal("Customer changed plans", order.CancelReason);
        Assert.NotNull(order.CancelledAt);
        var cancellation = Assert.Single(await finalDb.OrderStatusHistories.Where(change => change.OrderId == order.Id && change.ToStatus == "cancelled").ToArrayAsync());
        Assert.Equal(new EntityId(managerLogin.User.Id), cancellation.ChangedBy);
        var assignment = Assert.Single(await finalDb.DeliveryStatusHistories.ToArrayAsync());
        Assert.Equal(new EntityId(managerLogin.User.Id), assignment.ActorId);
        Assert.Equal("assigned", assignment.ToStatus);
        Assert.Empty(await finalDb.StockMovements.ToArrayAsync());
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

    private sealed record CustomerRequest(string FullName, string Phone, string? Email);
    private sealed record CustomerResponse(Guid Id, string FullName, string Phone, string? Email, bool IsActive, int LoyaltyPointsBalance);
    private sealed record CustomerAddressRequest(string AddressLine, string? Label, bool IsDefault);
    private sealed record CustomerAddressResponse(Guid Id, string AddressLine, string? Label, bool IsDefault, bool IsActive);
    private sealed record CustomerDetailResponse(Guid Id, string FullName, string Phone, string? Email, bool IsActive,
        int LoyaltyPointsBalance, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
        IReadOnlyList<CustomerAddressResponse> Addresses, IReadOnlyList<object> RecentOrders);
}
