namespace CoffeeShop.Infrastructure.Postgres;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Infrastructure.Postgres.Common;
using CoffeeShop.Infrastructure.Postgres.Inventory;
using Microsoft.Extensions.DependencyInjection;

public static class PostgresServiceCollectionExtensions
{
    public static IServiceCollection AddCoffeeShopPostgres(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<CoffeeShopDbContext>(options =>
            CoffeeShopPostgresOptions.Configure(options, connectionString));
        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<CoffeeShopDbContext>());
        services.AddScoped<ITransactionManager, PostgresTransactionManager>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();

        return services;
    }
}
