namespace CoffeeShop.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CoffeeShop.Contracts;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

public sealed class TableHttpTests : IAsyncLifetime
{
    private readonly string databaseName = $"coffeeshop_tables_{Guid.NewGuid():N}";
    private string? adminConnection;
    private WebApplicationFactory<Program>? host;

    public async Task InitializeAsync()
    {
        adminConnection = Environment.GetEnvironmentVariable("COFFEESHOP_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        await using (var connection = new NpgsqlConnection(adminConnection))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await create.ExecuteNonQueryAsync();
        }
        var connectionString = new NpgsqlConnectionStringBuilder(adminConnection)
        { Database = databaseName, Pooling = false }.ConnectionString;
        var options = new DbContextOptionsBuilder<CoffeeShopDbContext>();
        CoffeeShopPostgresOptions.Configure(options, connectionString);
        await using (var db = new CoffeeShopDbContext(options.Options)) await db.Database.MigrateAsync();
        host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseTestIdentity();
            builder.UseSetting("ConnectionStrings:CoffeeShop", connectionString);
            builder.ConfigureTestServices(services => services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { }));
        });
    }

    public async Task DisposeAsync()
    {
        if (host is not null) await host.DisposeAsync();
        if (adminConnection is null) return;
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task ManagerCreatesTable_AndCashierCanSelectItWithoutReseeding()
    {
        using var manager = Client("manager");
        using var cashier = Client("cashier");
        using var created = await manager.PostAsJsonAsync("/api/v1/tables", new CreateTableRequest(" T12 ", 4));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var table = await created.Content.ReadFromJsonAsync<PosTableResponse>();
        Assert.NotNull(table);
        Assert.Equal("T12", table.TableNumber);
        Assert.Equal(4, table.Seats);
        Assert.False(table.IsOccupied);

        var all = await cashier.GetFromJsonAsync<PosTableResponse[]>("/api/v1/pos/tables");
        Assert.Contains(all!, row => row.Id == table.Id && row.TableNumber == "T12" && !row.IsOccupied);
    }

    [PostgresFact]
    public async Task Create_RejectsInvalidDuplicateAndForbiddenRequestsWithoutMutation()
    {
        using var manager = Client("manager");
        using var cashier = Client("cashier");
        using var anonymous = host!.CreateClient();
        using var noAuth = await anonymous.PostAsJsonAsync("/api/v1/tables", new CreateTableRequest("T0", 2));
        using var forbidden = await cashier.PostAsJsonAsync("/api/v1/tables", new CreateTableRequest("T0", 2));
        using var invalidNumber = await manager.PostAsJsonAsync("/api/v1/tables", new CreateTableRequest(" ", 2));
        using var invalidSeats = await manager.PostAsJsonAsync("/api/v1/tables", new CreateTableRequest("T0", 0));
        using var tooLong = await manager.PostAsJsonAsync("/api/v1/tables", new CreateTableRequest(new string('X', 21), 2));
        Assert.Equal(HttpStatusCode.Unauthorized, noAuth.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidNumber.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSeats.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        using var first = await manager.PostAsJsonAsync("/api/v1/tables", new CreateTableRequest("T0", 2));
        using var duplicate = await manager.PostAsJsonAsync("/api/v1/tables", new CreateTableRequest(" T0 ", 4));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Single((await cashier.GetFromJsonAsync<PosTableResponse[]>("/api/v1/pos/tables"))!);
    }

    [PostgresFact]
    public async Task ConcurrentCreate_OnlyOneTableNumberIsPersisted()
    {
        using var firstClient = Client("manager");
        using var secondClient = Client("manager");
        var requests = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/api/v1/tables", new CreateTableRequest("T99", 2)),
            secondClient.PostAsJsonAsync("/api/v1/tables", new CreateTableRequest("T99", 4)));
        using var first = requests[0];
        using var second = requests[1];
        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict],
            requests.Select(response => response.StatusCode).OrderBy(status => status).ToArray());
        var rows = await firstClient.GetFromJsonAsync<PosTableResponse[]>("/api/v1/pos/tables");
        Assert.Single(rows!, row => row.TableNumber == "T99");
    }

    private HttpClient Client(string role)
    {
        var client = host!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", role);
        return client;
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
            if (role == "manager") claims.Add(new Claim("permission", "tables.manage"));
            claims.Add(new Claim("permission", "orders.create"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), "Test")));
        }
    }
}
