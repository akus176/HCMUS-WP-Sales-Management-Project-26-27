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
        Assert.False(document.RootElement.GetProperty("paths").TryGetProperty("/api/v1/preparation-tickets", out _));
        Assert.False(document.RootElement.GetProperty("components").GetProperty("schemas")
            .TryGetProperty("PreparationTicketResponse", out _));
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
}
