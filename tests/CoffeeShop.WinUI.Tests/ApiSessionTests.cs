using System.Net;
using System.Text;
using CoffeeShop.Contracts;
using CoffeeShop.WinUI.Security;
using CoffeeShop.WinUI.Services;

namespace CoffeeShop.WinUI.Tests;

public sealed class ApiSessionTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, ApiErrorKind.Validation)]
    [InlineData(HttpStatusCode.Unauthorized, ApiErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, ApiErrorKind.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, ApiErrorKind.NotFound)]
    [InlineData(HttpStatusCode.Conflict, ApiErrorKind.Conflict)]
    [InlineData((HttpStatusCode)422, ApiErrorKind.BusinessRule)]
    [InlineData(HttpStatusCode.InternalServerError, ApiErrorKind.Server)]
    public async Task ApiResponseParser_MapsStandardProblemResponses(
        HttpStatusCode status,
        ApiErrorKind expectedKind)
    {
        using var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(
                "{\"type\":\"about:blank\",\"title\":\"Request rejected\",\"status\":422,\"detail\":\"The request was rejected.\",\"errors\":{\"name\":[\"Required\"]}}",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            ApiResponseParser.ReadJsonAsync<TestPayload>(response));

        Assert.Equal(expectedKind, error.Kind);
        Assert.Equal("Request rejected", error.Title);
        Assert.Equal("The request was rejected.", error.Detail);
        Assert.Contains("Required", error.Errors["name"]);
    }

    [Fact]
    public async Task ApiResponseParser_ReadsTypedSuccessPayload()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"value\":\"server\"}", Encoding.UTF8, "application/json"),
        };

        var result = await ApiResponseParser.ReadJsonAsync<TestPayload>(response);

        Assert.Equal("server", result.Value);
    }

    [Fact]
    public async Task StaffIdentityApiClient_UsesPublishedLoginRefreshAndLogoutContracts()
    {
        var requests = new List<(string Method, string Path, string? Bearer, string Body)>();
        var payload = "{\"accessToken\":\"a\",\"refreshToken\":\"r\",\"expiresAt\":\"2026-11-05T01:00:00Z\",\"user\":{\"id\":\"05f27eef-cad5-475e-84a9-dbb0e89f4883\",\"fullName\":\"Casey Barista\",\"roles\":[\"Barista\"],\"permissions\":[\"orders.fulfill\"]}}";
        var handler = new SequenceHandler(async (request, cancellationToken) =>
        {
            requests.Add((request.Method.Method, request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.Parameter,
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return requests.Count < 3
                ? Json(HttpStatusCode.OK, payload)
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/api/v1/") };
        var identity = new StaffIdentityApiClient(http);

        await identity.LoginAsync("barista", "pw");
        await identity.RefreshAsync("refresh-token");
        await identity.LogoutAsync("refresh-token", "access-token");

        Assert.Equal(["/api/v1/auth/login", "/api/v1/auth/refresh", "/api/v1/auth/logout"], requests.Select(request => request.Path));
        Assert.Contains("\"username\":\"barista\"", requests[0].Body);
        Assert.Contains("\"refreshToken\":\"refresh-token\"", requests[1].Body);
        Assert.Equal("access-token", requests[2].Bearer);
    }

    [Fact]
    public async Task LoginAndRefresh_PersistOnlyTheRotatingRefreshToken()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-11-05T00:00:00Z"));
        var identity = new FakeIdentityApi(
            Token("access-1", "refresh-1", time.GetUtcNow().AddMinutes(30)),
            Token("access-2", "refresh-2", time.GetUtcNow().AddHours(1)));
        var store = new MemoryRefreshTokenStore();
        var session = new StaffSessionManager(identity, store, time);

        var user = await session.LoginAsync("barista", "test-password");
        Assert.Equal("Casey Barista", user.FullName);
        Assert.Equal("refresh-1", store.Value);
        Assert.Equal("access-1", await session.GetValidAccessTokenAsync());

        time.Advance(TimeSpan.FromMinutes(29));
        Assert.Equal("access-2", await session.GetValidAccessTokenAsync());
        Assert.Equal("refresh-2", store.Value);
        Assert.Equal(1, identity.RefreshCalls);
    }

    [Fact]
    public async Task ConcurrentUnauthorizedResponses_UseOneRefreshRotation()
    {
        var identity = new FakeIdentityApi(Token("access-1", "refresh-1"), Token("access-2", "refresh-2"));
        var session = new StaffSessionManager(identity, new MemoryRefreshTokenStore());
        await session.LoginAsync("barista", "test-password");

        var first = session.RefreshAfterUnauthorizedAsync("access-1");
        var second = session.RefreshAfterUnauthorizedAsync("access-1");

        Assert.Equal(new string?[] { "access-2", "access-2" }, await Task.WhenAll(first, second));
        Assert.Equal(1, identity.RefreshCalls);
    }

    [Fact]
    public async Task AuthenticatedApiClient_AttachesBearerAndRetriesOnceAfterRotatingRefresh()
    {
        var identity = new FakeIdentityApi(Token("access-1", "refresh-1"), Token("access-2", "refresh-2"));
        var session = new StaffSessionManager(identity, new MemoryRefreshTokenStore());
        await session.LoginAsync("barista", "test-password");
        var authorization = new List<string?>();
        var handler = new SequenceHandler((request, _) =>
        {
            authorization.Add(request.Headers.Authorization?.Parameter);
            return Task.FromResult(authorization.Count == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Json(HttpStatusCode.OK, "{\"value\":\"current\"}"));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/api/v1/") };
        var client = new AuthenticatedApiClient(http, session);

        var result = await client.GetAsync<TestPayload>("orders");

        Assert.Equal("current", result.Value);
        Assert.Equal(["access-1", "access-2"], authorization);
        Assert.Equal(1, identity.RefreshCalls);
    }

    [Fact]
    public async Task AuthenticatedApiClient_MapsTransportFailureToNetworkError()
    {
        var session = new StaffSessionManager(
            new FakeIdentityApi(Token("access-1", "refresh-1")),
            new MemoryRefreshTokenStore());
        await session.LoginAsync("barista", "test-password");
        using var http = new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("https://api.example.test/api/v1/"),
        };
        var client = new AuthenticatedApiClient(http, session);

        var error = await Assert.ThrowsAsync<ApiException>(() => client.GetAsync<TestPayload>("orders"));

        Assert.Equal(ApiErrorKind.Network, error.Kind);
    }

    [Fact]
    public async Task FailedRefreshAndLogoutClearPersistedCredentialsAndProtectedData()
    {
        var identity = new FakeIdentityApi(Token("access-1", "refresh-1"))
        {
            RefreshError = new ApiException(ApiErrorKind.Unauthorized, HttpStatusCode.Unauthorized, "Invalid session.", "Invalid session."),
        };
        var store = new MemoryRefreshTokenStore();
        var session = new StaffSessionManager(identity, store);
        var expirations = 0;
        session.SessionExpired += () => expirations++;
        await session.LoginAsync("barista", "test-password");

        Assert.Null(await session.RefreshAfterUnauthorizedAsync("access-1"));
        Assert.Null(store.Value);
        Assert.False(session.IsAuthenticated);
        Assert.Equal(1, expirations);

        await session.ClearLocalSessionAsync();
        Assert.Equal(1, expirations);

        await session.LoginAsync("barista", "test-password");
        await session.LogoutAsync();
        Assert.Null(store.Value);
        Assert.False(session.IsAuthenticated);
        Assert.Equal(1, expirations);
    }

    [Fact]
    public async Task ExpiredAccessAndRejectedRefresh_NotifyShellAndClearSession()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-11-05T00:00:00Z"));
        var identity = new FakeIdentityApi(Token("access", "refresh", time.GetUtcNow().AddMinutes(2)))
        {
            RefreshError = new ApiException(ApiErrorKind.Unauthorized, HttpStatusCode.Unauthorized, "Invalid session.", "Invalid session."),
        };
        var store = new MemoryRefreshTokenStore();
        var session = new StaffSessionManager(identity, store, time);
        var expirations = 0;
        session.SessionExpired += () => expirations++;
        await session.LoginAsync("barista", "test-password");

        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(await session.GetValidAccessTokenAsync());
        Assert.False(session.IsAuthenticated);
        Assert.Null(store.Value);
        Assert.Equal(1, expirations);
    }

    [Fact]
    public async Task LogoutTimeout_StillClearsLocalSession()
    {
        var identity = new FakeIdentityApi(Token("access", "refresh"))
        {
            LogoutError = new TaskCanceledException("Request timed out."),
        };
        var store = new MemoryRefreshTokenStore();
        var session = new StaffSessionManager(identity, store);
        await session.LoginAsync("barista", "test-password");

        await Assert.ThrowsAsync<TaskCanceledException>(() => session.LogoutAsync());
        Assert.False(session.IsAuthenticated);
        Assert.Null(store.Value);
    }

    [Fact]
    public async Task ProtectedRefreshTokenFileStore_EncryptsAtRestAndClearsOnLogout()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"coffee-shop-token-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "refresh.bin");
        var store = new ProtectedRefreshTokenFileStore(path, new TestProtector());
        const string secret = "refresh-token-must-not-be-plaintext";

        await store.SaveAsync(secret);
        var protectedBytes = await File.ReadAllBytesAsync(path);
        Assert.DoesNotContain(secret, Encoding.UTF8.GetString(protectedBytes));
        Assert.Equal(secret, await store.LoadAsync());
        await store.ClearAsync();
        Assert.Null(await store.LoadAsync());
        Assert.False(File.Exists(path));
        Directory.Delete(directory);
    }

    private static TokenResponse Token(string access, string refresh) => new(
        access,
        refresh,
        DateTimeOffset.UtcNow.AddHours(1),
        new StaffSession(Guid.Parse("05f27eef-cad5-475e-84a9-dbb0e89f4883"), "Casey Barista", ["Barista"], ["orders.fulfill"]));

    private static TokenResponse Token(string access, string refresh, DateTimeOffset expiresAt) => new(
        access,
        refresh,
        expiresAt,
        new StaffSession(Guid.Parse("05f27eef-cad5-475e-84a9-dbb0e89f4883"), "Casey Barista", ["Barista"], ["orders.fulfill"]));

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed record TestPayload(string Value);

    private sealed class FakeIdentityApi(params TokenResponse[] responses) : IStaffIdentityApi
    {
        private readonly Queue<TokenResponse> responses = new(responses);
        private TokenResponse? lastResponse;
        public int RefreshCalls { get; private set; }
        public ApiException? RefreshError { get; init; }
        public Exception? LogoutError { get; init; }

        public Task<TokenResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(lastResponse = responses.Count > 0 ? responses.Dequeue() : lastResponse!);

        public Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            if (RefreshError is not null) return Task.FromException<TokenResponse>(RefreshError);
            return Task.FromResult(lastResponse = responses.Count > 0 ? responses.Dequeue() : lastResponse!);
        }

        public Task LogoutAsync(string refreshToken, string accessToken, CancellationToken cancellationToken = default) =>
            LogoutError is null ? Task.CompletedTask : Task.FromException(LogoutError);
    }

    private sealed class MemoryRefreshTokenStore : IRefreshTokenStore
    {
        public string? Value { get; private set; }
        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task SaveAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            Value = refreshToken;
            return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Value = null;
            return Task.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan delta) => now += delta;
    }

    private sealed class TestProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plaintext) => plaintext.Select(value => (byte)(value ^ 0xA5)).ToArray();
        public byte[] Unprotect(byte[] protectedData) => protectedData.Select(value => (byte)(value ^ 0xA5)).ToArray();
    }

    private sealed class SequenceHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Service unavailable."));
    }
}
