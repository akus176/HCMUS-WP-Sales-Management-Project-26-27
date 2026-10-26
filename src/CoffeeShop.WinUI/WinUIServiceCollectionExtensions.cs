using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Sales;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI;

internal static class WinUIServiceCollectionExtensions
{
    public static IServiceCollection AddCoffeeShopDirectClient(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCoffeeShopPostgres(connectionString);
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<CatalogCommandHandler>();
        services.AddScoped<CatalogQueryHandler>();
        services.AddScoped<RecipeCommandHandler>();
        services.AddScoped<CustomerCommandHandler>();
        services.AddScoped<CustomerQueryHandler>();
        services.AddScoped<CustomerOrderSelectionHandler>();
        services.AddSingleton<CustomerOrderContext>();
        services.AddSingleton<CustomerWorkspace>();
        services.AddSingleton<PosOperatorSession>();
        services.AddSingleton<ICurrentUser>(provider => provider.GetRequiredService<PosOperatorSession>());
        services.AddScoped<PosOrderHandler>();
        services.AddScoped<OrderConfirmationCommandHandler>();
        services.AddScoped<DiscountCommandHandler>();
        services.AddScoped<PaymentCommandHandler>();
        services.AddScoped<DeliveryCommandHandler>();
        services.AddScoped<OrderCancellationCommandHandler>();
        services.AddSingleton<PosWorkspace>();
        services.AddSingleton<OperationsWorkspace>();
        services.AddScoped<InventoryCommandHandler>();

        return services;
    }
}
