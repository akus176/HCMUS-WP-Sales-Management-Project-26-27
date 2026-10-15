namespace CoffeeShop.Infrastructure.Postgres;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Sales;
using CoffeeShop.Infrastructure.Postgres.Catalog;
using CoffeeShop.Infrastructure.Postgres.Common;
using CoffeeShop.Infrastructure.Postgres.Customers;
using CoffeeShop.Infrastructure.Postgres.Inventory;
using CoffeeShop.Infrastructure.Postgres.Sales;
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
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICatalogReadRepository, CatalogReadRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerReadRepository, CustomerReadRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IDiscountRepository, DiscountRepository>();
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        services.AddScoped<IOrderConfirmationRepository, OrderConfirmationRepository>();
        services.AddScoped<IOrderCancellationRepository, OrderCancellationRepository>();
        services.AddScoped<InventoryCommandHandler>();
        services.AddScoped<LoyaltyCommandHandler>();
        services.AddScoped<OrderCancellationCommandHandler>();

        return services;
    }
}
