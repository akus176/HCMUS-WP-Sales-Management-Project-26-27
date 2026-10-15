using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Sales;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using CoffeeShop.Infrastructure.Postgres.Inventory;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace CoffeeShop.WinUI;

/// <summary>
/// In-process composition root: hosts Application services and PostgreSQL
/// infrastructure directly in the WinUI client (midterm milestone mode).
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;

    public static ServiceProvider Services { get; private set; } = null!;

    public Window? Window => _window;

    public App()
    {
        InitializeComponent();
        Services = BuildServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    private static ServiceProvider BuildServices()
    {
        var connectionString = Environment.GetEnvironmentVariable(CoffeeShopPostgresOptions.ConnectionStringEnvironmentVariable)
            ?? "Host=localhost;Port=5432;Database=coffeeshop;Username=postgres;Password=postgres";

        var services = new ServiceCollection();
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
        services.AddSingleton<PosWorkspace>();
        services.AddScoped<InventoryCommandHandler>();
        services.AddScoped<IInventoryRepository>(serviceProvider =>
            serviceProvider.GetRequiredService<InventoryRepository>());
        return services.BuildServiceProvider();
    }
}
