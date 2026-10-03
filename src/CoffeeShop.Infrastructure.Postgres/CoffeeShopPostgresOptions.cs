namespace CoffeeShop.Infrastructure.Postgres;

using Microsoft.EntityFrameworkCore;

public static class CoffeeShopPostgresOptions
{
    public const string ConnectionStringEnvironmentVariable = "ConnectionStrings__CoffeeShop";

    public static void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A PostgreSQL connection string is required.", nameof(connectionString));
        }

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(CoffeeShopDbContext).Assembly.FullName));
    }
}
