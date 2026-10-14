namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

/// <summary>
/// Integration tests proving the low-stock report returns exactly the same
/// rows as the inventory low-stock query against persisted ingredients.
/// Requires PostgreSQL reachable at COFFEESHOP_TEST_POSTGRES; tests skip
/// otherwise.
/// </summary>
public sealed class LowStockReportIntegrationTests : IAsyncLifetime
{
    public const string ConnectionVariable = CatalogPersistenceIntegrationTests.ConnectionVariable;
    private readonly string databaseName = $"coffeeshop_lowstock_{Guid.NewGuid():N}";
    private ServiceProvider services = null!;
    private string adminConnectionString = null!;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        adminConnectionString = configured;
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = databaseName, Pooling = false };
        services = new ServiceCollection().AddCoffeeShopPostgres(builder.ConnectionString).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (services is not null)
        {
            await services.DisposeAsync();
        }

        if (adminConnectionString is not null)
        {
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    [PostgresFact]
    public async Task Report_MatchesInventoryQueryOnPersistedIngredients()
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        context.Ingredients.AddRange(
            new Ingredient(EntityId.New(), "Above", "g", now, now, availableQuantity: 11m, reorderThreshold: 10m),
            new Ingredient(EntityId.New(), "Equal", "g", now, now, availableQuantity: 10m, reorderThreshold: 10m),
            new Ingredient(EntityId.New(), "Below", "ml", now, now, availableQuantity: 9m, reorderThreshold: 10m),
            new Ingredient(EntityId.New(), "Inactive", "g", now, now, availableQuantity: 0m, reorderThreshold: 10m, isActive: false));
        await context.SaveChangesAsync();

        var repository = services.CreateScope().ServiceProvider.GetRequiredService<IInventoryRepository>();
        var ingredients = await repository.ListIngredientsAsync();

        var report = LowStockReport.Build(ingredients);
        var inventory = LowStockQuery.Find(ingredients);

        Assert.Equal(inventory, report);
        Assert.Equal(2, report.Count);
        Assert.Equal(["Below", "Equal"], report.Select(row => row.Name).ToArray());
        Assert.DoesNotContain(report, row => row.Name == "Inactive");
        Assert.DoesNotContain(report, row => row.Name == "Above");
    }

    [PostgresFact]
    public async Task Report_IdentifiesBalanceThresholdAndUnitFromPersistence()
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var beans = new Ingredient(EntityId.New(), "Beans", "g", now, now, availableQuantity: 1.5m, reorderThreshold: 2m);
        context.Ingredients.Add(beans);
        await context.SaveChangesAsync();

        var repository = services.CreateScope().ServiceProvider.GetRequiredService<IInventoryRepository>();
        var report = LowStockReport.Build(await repository.ListIngredientsAsync());

        var row = Assert.Single(report);
        Assert.Equal(beans.Id, row.Id);
        Assert.Equal("Beans", row.Name);
        Assert.Equal("g", row.BaseUnit);
        Assert.Equal(1.5m, row.AvailableQuantity);
        Assert.Equal(2m, row.ReorderThreshold);
    }

    [PostgresFact]
    public async Task Report_ExcludesIngredientsDeactivatedAfterSeeding()
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var beans = new Ingredient(EntityId.New(), "Beans", "g", now, now, availableQuantity: 1m, reorderThreshold: 2m);
        context.Ingredients.Add(beans);
        await context.SaveChangesAsync();

        var repository = services.CreateScope().ServiceProvider.GetRequiredService<IInventoryRepository>();
        Assert.Single(LowStockReport.Build(await repository.ListIngredientsAsync()));

        // Deactivate through a fresh scope so the change is persisted and reloaded.
        await using var updateScope = services.CreateAsyncScope();
        var updateContext = updateScope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var tracked = await updateContext.Ingredients.SingleAsync(i => i.Id == beans.Id);
        tracked.Deactivate(now);
        await updateContext.SaveChangesAsync();

        // Read through a fresh scope; the previous scope still tracks the active instance.
        var freshRepository = services.CreateScope().ServiceProvider.GetRequiredService<IInventoryRepository>();
        Assert.Empty(LowStockReport.Build(await freshRepository.ListIngredientsAsync()));
    }
}
