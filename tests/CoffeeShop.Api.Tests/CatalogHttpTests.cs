using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CoffeeShop.Api.Tests;

public sealed class CatalogHttpTests : IAsyncLifetime
{
    private readonly string databaseName = $"coffeeshop_api_{Guid.NewGuid():N}";
    private string? adminConnection;
    private string? testConnection;
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
        testConnection = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName, Pooling = false }.ConnectionString;
        var options = new DbContextOptionsBuilder<CoffeeShopDbContext>();
        CoffeeShopPostgresOptions.Configure(options, testConnection);
        await using (var db = new CoffeeShopDbContext(options.Options)) await db.Database.MigrateAsync();
        host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseTestIdentity();
            builder.UseSetting("ConnectionStrings:CoffeeShop", testConnection);
            builder.ConfigureTestServices(services => services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { }));
        });
    }

    public async Task DisposeAsync()
    {
        if (host is not null) await host.DisposeAsync();
        if (adminConnection is not null)
        {
            await using var connection = new NpgsqlConnection(adminConnection);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    [PostgresFact]
    public async Task CategoryRoutes_MapSuccessValidationNotFoundAndConflict()
    {
        using var client = host!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "manager");
        var name = $"API category {Guid.NewGuid():N}";
        using var created = await client.PostAsJsonAsync("/api/v1/categories", new { name, displayOrder = 2 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<CreatedCategory>())!.Id;

        using var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        using var listed = await client.GetAsync($"/api/v1/categories?search={Uri.EscapeDataString(name)}");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Contains(name, await listed.Content.ReadAsStringAsync());

        using var invalid = await client.PostAsJsonAsync("/api/v1/categories", new { name = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType!.MediaType);
        using (var problem = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync()))
        {
            Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("catalog.invalid_input", problem.RootElement.GetProperty("code").GetString());
            Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("name", out _));
        }

        using var absent = await client.PostAsync($"/api/v1/categories/{Guid.NewGuid()}/deactivate", null);
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);

        using var deactivated = await client.PostAsync($"/api/v1/categories/{id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        using var repeated = await client.PostAsync($"/api/v1/categories/{id}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        using var conflict = JsonDocument.Parse(await repeated.Content.ReadAsStringAsync());
        Assert.Equal("catalog.invalid_state", conflict.RootElement.GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task CategoryWrites_RequireAuthenticationAndPermission()
    {
        using var client = host!.CreateClient();
        using var anonymous = await client.PostAsJsonAsync("/api/v1/categories", new { name = "Denied" });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("application/problem+json", anonymous.Content.Headers.ContentType?.MediaType);

        client.DefaultRequestHeaders.Add("X-Test-User", "reader");
        using var reader = await client.PostAsJsonAsync("/api/v1/categories", new { name = "Denied" });
        Assert.Equal(HttpStatusCode.Forbidden, reader.StatusCode);
        Assert.Equal("application/problem+json", reader.Content.Headers.ContentType?.MediaType);
    }

    private sealed record CreatedCategory(Guid Id);

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var kind))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
            if (kind == "manager") claims.Add(new Claim("permission", "menu.manage"));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
        }
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COFFEESHOP_TEST_POSTGRES")))
            Skip = "Set COFFEESHOP_TEST_POSTGRES to run PostgreSQL API tests.";
    }
}
