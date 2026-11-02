using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoffeeShop.Api.Tests;

public sealed class HostTests
{
    private static WebApplicationFactory<Program> CreateHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:CoffeeShop",
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
        Assert.False(document.RootElement.GetProperty("paths").TryGetProperty("/api/v1/auth/login", out _));
        var categoryPath = document.RootElement.GetProperty("paths").GetProperty("/api/v1/categories");
        Assert.True(categoryPath.TryGetProperty("get", out _));
        Assert.True(categoryPath.TryGetProperty("post", out _));
        Assert.True(document.RootElement.GetProperty("components").GetProperty("schemas")
            .TryGetProperty("CategoryResponse", out _));
    }
}
