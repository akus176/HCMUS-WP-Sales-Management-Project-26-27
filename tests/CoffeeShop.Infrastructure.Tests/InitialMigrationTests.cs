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
    public void InitialMigration_IsTheFirstRegisteredMigration()
    {
        using var context = CreateContext();

        var migration = context.Database.GetMigrations().First();

        Assert.EndsWith("_InitialCreate", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Migrations_MatchTheCurrentModel()
    {
        using var context = CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void DeliveryHistoryMigration_IsRegisteredAndSupportsEveryDeliveryStatus()
    {
        using var context = CreateContext();
        Assert.Contains("20261010060000_AddDeliveryStatusHistory", context.Database.GetMigrations());
        foreach (var (type, propertyName) in new[]
        {
            (typeof(Domain.Sales.Delivery), nameof(Domain.Sales.Delivery.Status)),
            (typeof(Domain.Sales.DeliveryStatusHistory), nameof(Domain.Sales.DeliveryStatusHistory.FromStatus)),
            (typeof(Domain.Sales.DeliveryStatusHistory), nameof(Domain.Sales.DeliveryStatusHistory.ToStatus))
        })
        {
            var length = context.Model.FindEntityType(type)!.FindProperty(propertyName)!.GetMaxLength();
            Assert.True(length >= Domain.Sales.DeliveryStatus.OutForDelivery.Length);
        }
    }

    [Fact]
    public void ReservationMigration_OnlyReplacesTheActiveReservationIndex()
    {
        using var context = CreateContext();
        var migrations = context.Database.GetMigrations().ToArray();
        var script = context.GetService<IMigrator>().GenerateScript(migrations[0], migrations[1]);

        Assert.EndsWith("_FilterActiveReservations", migrations[1], StringComparison.Ordinal);
        Assert.Contains("DROP INDEX sales.\"IX_reservations_table_id_reserved_for\"", script, StringComparison.Ordinal);
        Assert.Contains("WHERE status NOT IN ('cancelled', 'completed')", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE", script, StringComparison.Ordinal);
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
