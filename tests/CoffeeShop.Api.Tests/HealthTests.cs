using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoffeeShop.Api.Tests;

public sealed class HealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseTestIdentity().UseSetting("ConnectionStrings:CoffeeShop",
            "Host=127.0.0.1;Port=1;Database=coffeeshop;Username=test;Password=test;Timeout=1"));
    }

    [Fact]
    public async Task HealthEndpointIsAvailable()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
