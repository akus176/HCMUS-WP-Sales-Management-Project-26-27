using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Sales;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoffeeShop.WinUI;

internal static class WinUIServiceCollectionExtensions
{
    public static IServiceCollection AddCoffeeShopDirectClient(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<PosOperatorSession>();
        services.TryAddSingleton<ICurrentUser>(provider => provider.GetRequiredService<PosOperatorSession>());
        if (string.IsNullOrWhiteSpace(connectionString)) return services;
        services.AddCoffeeShopPostgres(connectionString);
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<CatalogCommandHandler>();
        services.AddScoped<CatalogQueryHandler>();
        services.AddScoped<RecipeCommandHandler>();
        services.AddSingleton<ICatalogWorkspaceSource, DirectCatalogWorkspaceSource>();
        services.AddScoped<CustomerCommandHandler>();
        services.AddScoped<CustomerQueryHandler>();
        services.AddScoped<CustomerOrderSelectionHandler>();
        services.AddSingleton<CustomerOrderContext>();
        services.AddSingleton<ICustomerWorkspaceSource, DirectCustomerWorkspaceSource>();
        services.AddSingleton<ITableManagementSource, DirectTableManagementSource>();
        services.AddSingleton<TableManagementWorkspace>();
        services.AddSingleton<CustomerWorkspace>(provider => new CustomerWorkspace(
            provider.GetRequiredService<ICustomerWorkspaceSource>(), provider.GetRequiredService<CustomerOrderContext>()));
        services.AddScoped<PosOrderHandler>();
        services.AddScoped<OrderConfirmationCommandHandler>();
        services.AddScoped<DiscountCommandHandler>();
        services.AddScoped<PaymentCommandHandler>();
        services.AddScoped<DeliveryCommandHandler>();
        services.AddScoped<OrderCancellationCommandHandler>();
        services.AddSingleton<PosWorkspace>();
        services.AddSingleton<OperationsWorkspace>();
        services.AddSingleton<InventoryAuditWorkspace>();
        services.AddScoped<InventoryCommandHandler>();

        return services;
    }
}
