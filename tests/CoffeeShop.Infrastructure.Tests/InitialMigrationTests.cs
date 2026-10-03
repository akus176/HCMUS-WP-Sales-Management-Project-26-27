namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

public sealed class InitialMigrationTests
{
    private const string TestConnectionString =
        "Host=localhost;Port=5432;Database=coffeeshop_test;Username=test;Password=test";

    [Fact]
    public void InitialMigration_IsTheOnlyRegisteredMigration()
    {
        using var context = CreateContext();

        var migration = Assert.Single(context.Database.GetMigrations());

        Assert.EndsWith("_InitialCreate", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void InitialMigrationScript_CapturesSchemasExtensionAndDatabaseConstraints()
    {
        using var context = CreateContext();

        var script = context.GetService<IMigrator>().GenerateScript();

        Assert.Contains("CREATE SCHEMA catalog", script, StringComparison.Ordinal);
        Assert.Contains("CREATE SCHEMA customers", script, StringComparison.Ordinal);
        Assert.Contains("CREATE SCHEMA identity", script, StringComparison.Ordinal);
        Assert.Contains("CREATE SCHEMA inventory", script, StringComparison.Ordinal);
        Assert.Contains("CREATE SCHEMA sales", script, StringComparison.Ordinal);
        Assert.Contains("CREATE EXTENSION IF NOT EXISTS pgcrypto", script, StringComparison.Ordinal);
        Assert.Contains("numeric(12,2)", script, StringComparison.Ordinal);
        Assert.Contains("CHECK (", script, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX", script, StringComparison.Ordinal);
        Assert.Contains("FOREIGN KEY", script, StringComparison.Ordinal);
    }

    private static CoffeeShopDbContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CoffeeShopDbContext>();
        CoffeeShopPostgresOptions.Configure(optionsBuilder, TestConnectionString);
        return new CoffeeShopDbContext(optionsBuilder.Options);
    }
}
