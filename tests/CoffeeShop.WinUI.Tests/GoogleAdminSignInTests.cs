using System.Net;
using System.Net.Http.Json;
using CoffeeShop.Contracts;
using CoffeeShop.WinUI.Services;

namespace CoffeeShop.WinUI.Tests;

public sealed class GoogleAdminSignInTests
{
    [Fact]
    public async Task SystemBrowserCallbackRequiresStateAndSavesOnlyTheApplicationRefreshToken()
    {
        var user = new StaffSession(Guid.NewGuid(), "Test Administrator", ["Admin"], ["users.manage"]);
        var pair = new TokenResponse("test-app-access", "test-app-refresh", DateTimeOffset.UtcNow.AddMinutes(10), user);
        GoogleAdminLoginRequest? exchange = null;
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("/api/v1/auth/google/config", request.RequestUri!.AbsolutePath);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new GoogleAdminConfigResponse("test-desktop")) };
            }
            Assert.Equal("/api/v1/auth/google", request.RequestUri!.AbsolutePath);
            exchange = await request.Content!.ReadFromJsonAsync<GoogleAdminLoginRequest>(ct);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(pair) };
        })) { BaseAddress = new("https://coffeeshop.example.test/api/v1/") };
        var api = new StaffIdentityApiClient(http);
        var store = new MemoryStore();
        var session = new StaffSessionManager(api, store);
        var browser = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var login = new GoogleAdminSignIn(api, session).LoginAsync(uri => browser.SetResult(uri), timeout.Token);

        var address = await browser.Task.WaitAsync(timeout.Token);
        Assert.Equal("https://accounts.google.com/o/oauth2/v2/auth", address.GetLeftPart(UriPartial.Path));
        var query = address.Query[1..].Split('&').Select(part => part.Split('=', 2))
            .ToDictionary(part => part[0], part => Uri.UnescapeDataString(part[1]));
        Assert.Equal("test-desktop", query["client_id"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal(query["code_challenge"], query["nonce"]);
        Assert.DoesNotContain("code_verifier", query.Keys);
        using var callback = new HttpClient(new HttpClientHandler { UseProxy = false });
        var redirect = query["redirect_uri"];
        using var wrongState = await callback.GetAsync($"{redirect}?state=wrong&code=discard", timeout.Token);
        Assert.Equal(HttpStatusCode.BadRequest, wrongState.StatusCode);
        Assert.Null(exchange);
        using var valid = await callback.GetAsync($"{redirect}?state={query["state"]}&code=sample-code", timeout.Token);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        Assert.DoesNotContain("sample-code", await valid.Content.ReadAsStringAsync(timeout.Token));
        Assert.Equal(user.Id, (await login)!.Id);
        Assert.NotNull(exchange);
        Assert.Equal(redirect, exchange.RedirectUri);
        Assert.Equal("sample-code", exchange.Code);
        Assert.Equal(query["code_challenge"], GoogleAdminOAuth.Challenge(exchange.CodeVerifier!));
        Assert.Equal(pair.RefreshToken, store.Saved);
        Assert.Equal(pair.AccessToken, await session.GetValidAccessTokenAsync(timeout.Token));
    }

    [Fact]
    public async Task CancellationWhileWaitingForBrowserDoesNotCreateASession()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new GoogleAdminConfigResponse("test-desktop")),
        }))) { BaseAddress = new("https://coffeeshop.example.test/api/v1/") };
        var api = new StaffIdentityApiClient(http);
        var store = new MemoryStore();
        var session = new StaffSessionManager(api, store);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GoogleAdminSignIn(api, session).LoginAsync(_ => cancellation.Cancel(), cancellation.Token));
        Assert.False(session.IsAuthenticated);
        Assert.Null(store.Saved);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }

    private sealed class MemoryStore : IRefreshTokenStore
    {
        public string? Saved { get; private set; }
        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Saved);
        public Task SaveAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            Saved = refreshToken;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Saved = null;
            return Task.CompletedTask;
        }
    }
}
