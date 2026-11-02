namespace CoffeeShop.Api;

using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public sealed class PostgresHealthCheck(string connectionString) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = new DbContextOptionsBuilder<CoffeeShopDbContext>();
        CoffeeShopPostgresOptions.Configure(options, connectionString);
        await using var db = new CoffeeShopDbContext(options.Options);
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
        }
    }
}
