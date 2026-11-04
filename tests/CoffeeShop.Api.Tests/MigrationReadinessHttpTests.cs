namespace CoffeeShop.Api.Tests;

using System.Net;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public sealed class MigrationReadinessHttpTests
{
    [PostgresFact]
    public async Task ReadinessRequiresAppliedMigrations()
    {
        var adminConnection = Environment.GetEnvironmentVariable("COFFEESHOP_TEST_POSTGRES")!;
        var databaseName = $"coffeeshop_readiness_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(adminConnection))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            var connectionString = new NpgsqlConnectionStringBuilder(adminConnection)
            {
                Database = databaseName,
                Pooling = false,
            }.ConnectionString;
            using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseTestIdentity().UseSetting("ConnectionStrings:CoffeeShop", connectionString));
            using var client = host.CreateClient();

            using var live = await client.GetAsync("/health");
            using var before = await client.GetAsync("/health/ready");
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, before.StatusCode);

            var options = new DbContextOptionsBuilder<CoffeeShopDbContext>();
            CoffeeShopPostgresOptions.Configure(options, connectionString);
            await using (var db = new CoffeeShopDbContext(options.Options))
                await db.Database.MigrateAsync();

            using var after = await client.GetAsync("/health/ready");
            Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        }
        finally
        {
            await using var connection = new NpgsqlConnection(adminConnection);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
