using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoffeeShop.Api.Tests;

public sealed class HostTests
{
    [Fact]
    public void MissingSigningConfigurationFailsWithoutEchoingValues()
    {
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseTestIdentity().UseSetting("Jwt:SigningKey", "")
                .UseSetting("ConnectionStrings:CoffeeShop", "Host=127.0.0.1;Port=1;Database=test;Username=test"));
        var error = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("Configure Jwt:Issuer", error.Message);
        Assert.DoesNotContain(TestIdentitySettings.SigningKey, error.Message);
    }

    private static WebApplicationFactory<Program> CreateHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseTestIdentity().UseSetting("ConnectionStrings:CoffeeShop",
                "Host=127.0.0.1;Port=1;Database=coffeeshop;Username=test;Password=test;Timeout=1"));

    [Fact]
    public async Task ReadinessChecksDatabaseRatherThanOnlyProcess()
    {
        using var host = CreateHost();
        using var client = host.CreateClient();
        using var live = await client.GetAsync("/health");
        using var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }

    [Fact]
    public async Task OpenApiIsIndependentOfRequestOrigin()
    {
        using var host = CreateHost();
        using var first = host.CreateClient(new() { BaseAddress = new Uri("http://first.example/") });
        using var second = host.CreateClient(new() { BaseAddress = new Uri("http://second.example/") });

        var firstDocument = await first.GetStringAsync("/openapi/v1.json");
        var secondDocument = await second.GetStringAsync("/openapi/v1.json");
        Assert.Equal(firstDocument, secondDocument);
    }

    [Fact]
    public async Task OpenApiOnlyDescribesMappedBusinessRoutes()
    {
        using var host = CreateHost();
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/v1/categories", out _));
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/v1/auth/login", out _));
        var categoryPath = document.RootElement.GetProperty("paths").GetProperty("/api/v1/categories");
        Assert.True(categoryPath.TryGetProperty("get", out _));
        Assert.True(categoryPath.TryGetProperty("post", out _));
        Assert.True(document.RootElement.GetProperty("components").GetProperty("schemas")
            .TryGetProperty("CategoryResponse", out _));
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/v1/preparation-tickets", out _));
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/v1/courier/assignments", out _));
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/v1/ingredients", out _));
    }

    [Fact]
    public async Task MobileP0OpenApiMatchesPublishedResponsesAndPrivacyBoundary()
    {
        using var host = CreateHost();
        using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var (path, schema) in new[]
        {
            ("/api/v1/preparation-tickets", "PageResponseOfPreparationTicketResponse"),
            ("/api/v1/discounts/pending", "PageResponseOfPendingDiscountResponse"),
            ("/api/v1/ingredients", "PageResponseOfIngredientBalanceResponse"),
            ("/api/v1/ingredients/low-stock", "PageResponseOfIngredientBalanceResponse"),
            ("/api/v1/courier/assignments", "PageResponseOfCourierAssignmentResponse"),
        })
        {
            var operation = paths.GetProperty(path).GetProperty("get");
            Assert.Equal($"#/components/schemas/{schema}", operation.GetProperty("responses")
                .GetProperty("200").GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString());
            Assert.True(operation.GetProperty("security")[0].TryGetProperty("Bearer", out _));
            Assert.Contains(operation.GetProperty("parameters").EnumerateArray(), parameter =>
                parameter.GetProperty("name").GetString() == "pageSize");
            Assert.True(operation.GetProperty("responses").TryGetProperty("400", out _));
        }

        foreach (var path in new[] { "/api/v1/preparation-tickets/{id}/start",
            "/api/v1/preparation-tickets/{id}/ready", "/api/v1/courier/assignments/{id}/start",
            "/api/v1/courier/assignments/{id}/complete", "/api/v1/courier/assignments/{id}/fail" })
        {
            var operation = paths.GetProperty(path).GetProperty("post");
            Assert.True(operation.GetProperty("responses").TryGetProperty("204", out _));
            Assert.True(operation.GetProperty("responses").TryGetProperty("409", out _));
            Assert.True(operation.GetProperty("security")[0].TryGetProperty("Bearer", out _));
        }

        var ticket = schemas.GetProperty("PreparationTicketResponse").GetProperty("properties");
        Assert.False(ticket.TryGetProperty("customerId", out _));
        Assert.False(ticket.TryGetProperty("contactNumber", out _));
        var courier = schemas.GetProperty("CourierAssignmentResponse").GetProperty("properties");
        Assert.False(courier.TryGetProperty("contactNumber", out _));
        Assert.False(courier.TryGetProperty("addressLine", out _));
        Assert.True(schemas.GetProperty("CourierAssignmentDetailResponse").GetProperty("properties")
            .TryGetProperty("addressLine", out _));
        var adjustment = schemas.GetProperty("StockAdjustmentRequest").GetProperty("properties");
        Assert.True(adjustment.TryGetProperty("requestId", out _));
        Assert.False(adjustment.TryGetProperty("actorId", out _));
        Assert.Equal("uuid", schemas.GetProperty("PaymentResponse").GetProperty("properties")
            .GetProperty("paymentId").GetProperty("format").GetString());
    }

    [Fact]
    public async Task CustomerAndLoyaltyOpenApi_ExposesProtectedRoutesAndContracts()
    {
        using var host = CreateHost();
        using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        foreach (var (path, method) in new[]
        {
            ("/api/v1/customers", "get"),
            ("/api/v1/customers", "post"),
            ("/api/v1/customers/{id}", "get"),
            ("/api/v1/customers/{id}", "patch"),
            ("/api/v1/customers/{id}/addresses", "get"),
            ("/api/v1/customers/{id}/addresses", "post"),
            ("/api/v1/customers/{id}/loyalty-transactions", "get"),
            ("/api/v1/loyalty-settings", "get"),
            ("/api/v1/loyalty-settings", "patch"),
            ("/api/v1/orders/{id}/loyalty/redeem", "post"),
        })
        {
            var operation = paths.GetProperty(path).GetProperty(method);
            Assert.True(operation.GetProperty("security")[0].TryGetProperty("Bearer", out _), $"{method} {path} must require bearer authentication.");
        }

        var redeem = paths.GetProperty("/api/v1/orders/{id}/loyalty/redeem").GetProperty("post");
        Assert.True(redeem.GetProperty("responses").TryGetProperty("422", out _));
        foreach (var schema in new[] { "CustomerDetailResponse", "CustomerAddressResponse",
            "LoyaltyHistoryResponse", "LoyaltySettingsResponse", "LoyaltyOperationResponse" })
        {
            Assert.True(schemas.TryGetProperty(schema, out _), $"Missing OpenAPI schema {schema}.");
        }
    }

    [Fact]
    public async Task OpenApiDescribesBearerOnProtectedRoutesOnly()
    {
        using var host = CreateHost();
        using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;

        var bearer = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.True(root.GetProperty("paths").GetProperty("/api/v1/auth/me")
            .GetProperty("get").GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.True(root.GetProperty("paths").GetProperty("/api/v1/categories")
            .GetProperty("post").GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.False(root.GetProperty("paths").GetProperty("/api/v1/auth/login")
            .GetProperty("post").TryGetProperty("security", out _));
        Assert.True(root.GetProperty("paths").GetProperty("/api/v1/auth/me")
            .GetProperty("get").GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(root.GetProperty("paths").GetProperty("/api/v1/categories")
            .GetProperty("post").GetProperty("responses").TryGetProperty("403", out _));
    }

    [Fact]
    public async Task UnauthenticatedRequestsToProtectedRoutes_AreRejectedBeforeDatabaseAccess()
    {
        using var host = CreateHost();
        using var client = host.CreateClient();

        using var identityResponse = await client.GetAsync("/api/v1/auth/me");
        using var businessResponse = await client.GetAsync("/api/v1/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, identityResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, businessResponse.StatusCode);
    }
}
