namespace CoffeeShop.Infrastructure.Postgres;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class CoffeeShopDbContextFactory : IDesignTimeDbContextFactory<CoffeeShopDbContext>
{
    public CoffeeShopDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            CoffeeShopPostgresOptions.ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set the {CoffeeShopPostgresOptions.ConnectionStringEnvironmentVariable} environment variable " +
                "before running Entity Framework Core migration commands.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<CoffeeShopDbContext>();
        CoffeeShopPostgresOptions.Configure(optionsBuilder, connectionString);
        return new CoffeeShopDbContext(optionsBuilder.Options);
    }
}
