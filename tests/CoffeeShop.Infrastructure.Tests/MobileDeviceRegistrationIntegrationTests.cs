namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public sealed class MobileDeviceRegistrationIntegrationTests
{
    [PostgresFact]
    public async Task Migration_EnforcesUniqueActiveTokensAndAllowsReuseAfterRevocation()
    {
        var admin = Environment.GetEnvironmentVariable(CatalogPersistenceIntegrationTests.ConnectionVariable)!;
        var databaseName = $"coffeeshop_devices_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection))
            await create.ExecuteNonQueryAsync();

        try
        {
            var options = new DbContextOptionsBuilder<CoffeeShopDbContext>()
                .UseNpgsql(new NpgsqlConnectionStringBuilder(admin) { Database = databaseName, Pooling = false }.ConnectionString)
                .Options;
            await using var db = new CoffeeShopDbContext(options);
            await db.Database.MigrateAsync();
            var now = new UtcTimestamp(DateTimeOffset.UtcNow);
            var user = new User(EntityId.New(), "device_test", "device@example.test", "hash", "Device Test", null, now, now);
            db.Users.Add(user);
            var first = new MobileDeviceRegistration(EntityId.New(), user.Id, "android", "test-token", now);
            db.MobileDeviceRegistrations.Add(first);
            await db.SaveChangesAsync();

            var duplicate = new MobileDeviceRegistration(EntityId.New(), user.Id, "android", "test-token", now);
            db.MobileDeviceRegistrations.Add(duplicate);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
            db.Entry(duplicate).State = EntityState.Detached;
            first.Revoke(now);
            await db.SaveChangesAsync();
            db.MobileDeviceRegistrations.Add(new MobileDeviceRegistration(EntityId.New(), user.Id, "android", "test-token", now));
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.MobileDeviceRegistrations.CountAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
