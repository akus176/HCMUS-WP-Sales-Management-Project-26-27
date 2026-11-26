using System.Net;
using System.Net.Http.Json;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Sales;
using CoffeeShop.WinUI.Services;

namespace CoffeeShop.WinUI.Tests;

public sealed class ApiOperationsSourceTests
{
    [Fact]
    public async Task Manager_LoadsDeliveryBoardAndAssignsDeliveryThroughAuthenticatedApi()
    {
        var orderId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var requests = new List<(string Method, string Path, string? Token, string Body)>();
        var assigned = false;
        using var http = CreateClient(async request =>
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync();
            requests.Add((request.Method.Method, request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.Parameter, body));
            return request.RequestUri.AbsolutePath switch
            {
                "/api/v1/orders" => Json(new[] { new OperationalOrderResponse(orderId, "D-101", "delivery",
                    "ready", 24m, 24m, null, assigned ? "assigned" : "unassigned", assigned ? staffId : null, []) }),
                "/api/v1/delivery-staff/options" => Json(new[] { new DeliveryStaffResponse(staffId, "Courier One") }),
                _ when request.RequestUri.AbsolutePath == $"/api/v1/orders/{orderId}/assign-delivery" => OnAssignment(request),
                _ => throw new InvalidOperationException($"Unexpected route {request.RequestUri.AbsolutePath}"),
            };

            HttpResponseMessage OnAssignment(HttpRequestMessage message)
            {
                assigned = true;
                return new(HttpStatusCode.NoContent);
            }
        });
        var session = CreateSession(operatorId, "orders.create", "delivery.assign");
        var source = new ApiOperationsSource(new AuthenticatedApiClient(http, session), session);

        var before = await source.LoadAsync();
        Assert.Equal("unassigned", Assert.Single(before.Orders).DeliveryStatus);
        Assert.Equal("Courier One", Assert.Single(before.DeliveryStaff).FullName);
        await source.AssignDeliveryAsync(new(orderId), new(staffId));
        var after = await source.LoadAsync();

        Assert.Equal("assigned", Assert.Single(after.Orders).DeliveryStatus);
        var assignment = Assert.Single(requests, request => request.Method == "POST");
        Assert.Equal($"/api/v1/orders/{orderId}/assign-delivery", assignment.Path);
        Assert.Equal($"{{\"staffId\":\"{staffId}\"}}", assignment.Body);
        Assert.DoesNotContain(requests, request => request.Path == "/api/v1/pos/tables");
        Assert.All(requests, request => Assert.Equal("fixture-access", request.Token));
    }

    [Fact]
    public async Task Manager_UsesApiForPaymentDiscountCancellationAndOrderTransitions()
    {
        var orderId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var requests = new List<(string Method, string Path, string? Token)>();
        using var http = CreateClient(request =>
        {
            requests.Add((request.Method.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter));
            return request.RequestUri.AbsolutePath switch
            {
                "/api/v1/orders" => Json(new[] { new OperationalOrderResponse(orderId, "D-104", "dine_in",
                    "confirmed", 24m, 0m, "T1", null, null, [Guid.NewGuid()]) }),
                "/api/v1/delivery-staff/options" => Json(new[] { new DeliveryStaffResponse(staffId, "Courier One") }),
                _ => new(HttpStatusCode.NoContent),
            };
        });
        var session = CreateSession(Guid.NewGuid(), "orders.create", "orders.confirm", "orders.fulfill", "orders.complete",
            "payments.record", "discounts.request", "discounts.approve", "orders.cancel", "delivery.assign");
        var source = new ApiOperationsSource(new AuthenticatedApiClient(http, session), session);
        var discountId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var snapshot = await source.LoadAsync();
        await source.MoveAsync(new(orderId), "preparing");
        await source.MoveAsync(new(orderId), "ready");
        await source.RecordPaymentAsync(new(paymentId), new(orderId), "cash", 24m, "drawer-7");
        await source.MoveAsync(new(orderId), OrderStatus.Completed);
        await source.DecideDiscountAsync(new(discountId), approve: true);
        await source.CancelAsync(new(orderId), "Customer changed their mind");

        Assert.Equal(staffId, Assert.Single(snapshot.DeliveryStaff).Id.Value);
        Assert.Contains(requests, request => request.Method == "GET" && request.Path == "/api/v1/orders");
        Assert.DoesNotContain(requests, request => request.Path == "/api/v1/pos/tables");
        Assert.Contains(requests, request => request.Method == "POST" && request.Path == $"/api/v1/orders/{orderId}/preparing");
        Assert.Contains(requests, request => request.Method == "POST" && request.Path == $"/api/v1/orders/{orderId}/ready");
        Assert.Contains(requests, request => request.Method == "POST" && request.Path == $"/api/v1/orders/{orderId}/complete");
        Assert.Contains(requests, request => request.Method == "POST" && request.Path == $"/api/v1/orders/{orderId}/payments");
        Assert.Contains(requests, request => request.Method == "POST" && request.Path == $"/api/v1/discounts/{discountId}/approve");
        Assert.Contains(requests, request => request.Method == "POST" && request.Path == $"/api/v1/orders/{orderId}/cancel");
        Assert.All(requests, request => Assert.Equal("fixture-access", request.Token));
    }

    [Fact]
    public async Task Barista_LoadsFulfillmentBoardAndSendsStateMovesThroughHttp()
    {
        var orderId = Guid.NewGuid();
        var requests = new List<(string Method, string Path, string? Token)>();
        using var http = CreateClient(request =>
        {
            requests.Add((request.Method.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter));
            return request.RequestUri.AbsolutePath == "/api/v1/operations/orders"
                ? Json(new[] { new OperationalOrderResponse(orderId, "T-105", "takeaway", "confirmed", 18m, 0m, null, null, null, []) })
                : new(HttpStatusCode.NoContent);
        });
        var session = CreateSession(Guid.NewGuid(), "orders.fulfill");
        var source = new ApiOperationsSource(new AuthenticatedApiClient(http, session), session);

        var snapshot = await source.LoadAsync();
        await source.MoveAsync(new(orderId), "preparing");
        await source.MoveAsync(new(orderId), "ready");

        Assert.Equal("confirmed", Assert.Single(snapshot.Orders).Status);
        Assert.Contains(requests, request => request.Method == "GET" && request.Path == "/api/v1/operations/orders");
        Assert.Contains(requests, request => request.Method == "POST" && request.Path == $"/api/v1/orders/{orderId}/preparing");
        Assert.Contains(requests, request => request.Method == "POST" && request.Path == $"/api/v1/orders/{orderId}/ready");
        Assert.All(requests, request => Assert.Equal("fixture-access", request.Token));
    }

    [Fact]
    public async Task CourierLoadsAssignmentsAndSendsEligibleTransitionsOnce()
    {
        var orderId = Guid.NewGuid();
        var failureOrderId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var requests = new List<(string Method, string Path, string? Token, string Body)>();
        var deliveryStatus = "assigned";
        var failureStatus = "out_for_delivery";
        using var http = CreateClient(async request =>
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync();
            requests.Add((request.Method.Method, request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.Parameter, body));
            return request.RequestUri.AbsolutePath switch
            {
                "/api/v1/courier/assignments" => Json(new PageResponse<CourierAssignmentResponse>(
                    ActiveAssignments(), 1, 200, ActiveAssignments().Length)),
                var path when path == $"/api/v1/courier/assignments/{orderId}" => Json(new CourierAssignmentDetailResponse(
                    orderId, "D-102", deliveryStatus, "Sample Recipient", "0900…0102", "Sanitized Test Address")),
                var path when path == $"/api/v1/courier/assignments/{failureOrderId}" => Json(new CourierAssignmentDetailResponse(
                    failureOrderId, "D-103", failureStatus, "Test Recipient", "0900…0103", "Sanitized Test Address")),
                var path when path == $"/api/v1/courier/assignments/{orderId}/start" => Transition(ref deliveryStatus, "assigned", "out_for_delivery"),
                var path when path == $"/api/v1/courier/assignments/{orderId}/complete" => Transition(ref deliveryStatus, "out_for_delivery", "delivered"),
                var path when path == $"/api/v1/courier/assignments/{failureOrderId}/fail" => Transition(ref failureStatus, "out_for_delivery", "failed"),
                _ => throw new InvalidOperationException($"Unexpected route {request.RequestUri.AbsolutePath}"),
            };
        });
        var session = CreateSession(staffId, "delivery.update");
        var source = new ApiOperationsSource(new AuthenticatedApiClient(http, session), session);

        var snapshot = await source.LoadAsync();
        var detail = await source.GetDetailAsync(new(orderId));
        await source.StartDeliveryAsync(new(orderId));
        await source.CompleteDeliveryAsync(new(orderId));
        await source.FailDeliveryAsync(new(failureOrderId), "Recipient unavailable");
        var reloaded = await source.LoadAsync();

        Assert.Equal("assigned", Assert.Single(snapshot.Orders, order => order.Id.Value == orderId).DeliveryStatus);
        Assert.Equal("Sanitized Test Address", detail.AddressLine);
        Assert.Equal(new[]
        {
            "GET /api/v1/courier/assignments?page=1&pageSize=200",
            $"GET /api/v1/courier/assignments/{orderId}",
            $"POST /api/v1/courier/assignments/{orderId}/start",
            $"POST /api/v1/courier/assignments/{orderId}/complete",
            $"POST /api/v1/courier/assignments/{failureOrderId}/fail",
            "GET /api/v1/courier/assignments?page=1&pageSize=200",
        }, requests.Select(request => $"{request.Method} {request.Path}"));
        Assert.Empty(reloaded.Orders);
        Assert.Equal("{\"reason\":\"Recipient unavailable\"}", Assert.Single(requests,
            request => request.Path.EndsWith("/fail", StringComparison.Ordinal)).Body);
        Assert.All(requests, request => Assert.Equal("fixture-access", request.Token));

        CourierAssignmentResponse[] ActiveAssignments()
        {
            var active = new List<CourierAssignmentResponse>();
            if (deliveryStatus is "assigned" or "out_for_delivery") active.Add(new(orderId, "D-102", deliveryStatus));
            if (failureStatus is "assigned" or "out_for_delivery") active.Add(new(failureOrderId, "D-103", failureStatus));
            return active.ToArray();
        }

        HttpResponseMessage Transition(ref string current, string expected, string next)
        {
            if (current != expected) return new(HttpStatusCode.Conflict);
            current = next;
            return new(HttpStatusCode.NoContent);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, ApiErrorKind.Forbidden)]
    [InlineData(HttpStatusCode.Conflict, ApiErrorKind.Conflict)]
    public async Task DeliveryTransitionPreservesUnauthorizedAndConflictResponses(
        HttpStatusCode status, ApiErrorKind expectedKind)
    {
        var orderId = Guid.NewGuid();
        var session = CreateSession(Guid.NewGuid(), "delivery.update");
        using var errorHttp = CreateClient(_ => new(status)
        {
            Content = JsonContent.Create(new { title = "Action rejected", detail = "Refresh the assignment and retry." }),
        });
        var source = new ApiOperationsSource(new AuthenticatedApiClient(errorHttp, session), session);

        var error = await Assert.ThrowsAsync<ApiException>(() => source.StartDeliveryAsync(new(orderId)));

        Assert.Equal(expectedKind, error.Kind);
        Assert.Equal(status, error.StatusCode);
    }

    private static StaffSessionManager CreateSession(Guid staffId, params string[] permissions)
    {
        var identity = new TestIdentity(new StaffSession(staffId, "Fixture Staff", [], permissions));
        var session = new StaffSessionManager(identity, new TestStore());
        session.LoginAsync("fixture", "fixture").GetAwaiter().GetResult();
        return session;
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new Handler(request => Task.FromResult(respond(request))))
        { BaseAddress = new Uri("https://example.test/api/v1/") };

    private static HttpClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) =>
        new(new Handler(respond)) { BaseAddress = new Uri("https://example.test/api/v1/") };

    private static HttpResponseMessage Json<T>(T payload) => new(HttpStatusCode.OK)
    { Content = JsonContent.Create(payload) };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }

    private sealed class TestIdentity(StaffSession user) : IStaffIdentityApi
    {
        public Task<TokenResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TokenResponse("fixture-access", "fixture-refresh", DateTimeOffset.UtcNow.AddHours(1), user));
        public Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task LogoutAsync(string refreshToken, string accessToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestStore : IRefreshTokenStore
    {
        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task SaveAsync(string refreshToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
