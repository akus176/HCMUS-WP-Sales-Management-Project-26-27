namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Catalog;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

[Collection("PostgreSQL configuration")]
public sealed class PostgresConfigurationTests
{
    private const string TestConnectionString =
        "Host=localhost;Port=5432;Database=coffeeshop_test;Username=test;Password=test";

    [Fact]
    public void Configure_UsesNpgsqlAndInfrastructureMigrationAssembly()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CoffeeShopDbContext>();

        CoffeeShopPostgresOptions.Configure(optionsBuilder, TestConnectionString);

        using var context = new CoffeeShopDbContext(optionsBuilder.Options);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Equal(
            typeof(CoffeeShopDbContext).Assembly,
            context.GetService<IMigrationsAssembly>().Assembly);
    }

    [Fact]
    public void Configure_BlankConnectionString_ThrowsArgumentException()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CoffeeShopDbContext>();

        Assert.Throws<ArgumentException>(() =>
            CoffeeShopPostgresOptions.Configure(optionsBuilder, " "));
    }

    [Fact]
    public void AddCoffeeShopPostgres_RegistersOneScopedContextForPersistenceServices()
    {
        var services = new ServiceCollection();
        services.AddCoffeeShopPostgres(TestConnectionString);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();

        Assert.Same(context, scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.IsType<PostgresTransactionManager>(
            scope.ServiceProvider.GetRequiredService<ITransactionManager>());
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
        var catalogReads = scope.ServiceProvider.GetRequiredService<ICatalogReadRepository>();
        Assert.IsType<CatalogRepository>(catalog);
        Assert.IsType<CatalogReadRepository>(catalogReads);
        Assert.Same(catalog, scope.ServiceProvider.GetRequiredService<ICatalogRepository>());
        Assert.Same(catalogReads, scope.ServiceProvider.GetRequiredService<ICatalogReadRepository>());

        using var otherScope = serviceProvider.CreateScope();
        Assert.NotSame(catalog, otherScope.ServiceProvider.GetRequiredService<ICatalogRepository>());
        Assert.NotSame(catalogReads, otherScope.ServiceProvider.GetRequiredService<ICatalogReadRepository>());
    }

    [Fact]
    public void DesignTimeFactory_WithEnvironmentVariable_CreatesNpgsqlContext()
    {
        RunWithConnectionString(TestConnectionString, () =>
        {
            using var context = new CoffeeShopDbContextFactory().CreateDbContext([]);

            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        });
    }

    [Fact]
    public void DesignTimeFactory_WithoutEnvironmentVariable_ExplainsRequiredConfiguration()
    {
        RunWithConnectionString(null, () =>
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                new CoffeeShopDbContextFactory().CreateDbContext([]));

            Assert.Contains(CoffeeShopPostgresOptions.ConnectionStringEnvironmentVariable, exception.Message);
        });
    }

    private static void RunWithConnectionString(string? value, Action action)
    {
        var variableName = CoffeeShopPostgresOptions.ConnectionStringEnvironmentVariable;
        var originalValue = Environment.GetEnvironmentVariable(variableName);

        try
        {
            Environment.SetEnvironmentVariable(variableName, value);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, originalValue);
        }
    }
}

[CollectionDefinition("PostgreSQL configuration", DisableParallelization = true)]
public sealed class PostgresConfigurationCollection;
