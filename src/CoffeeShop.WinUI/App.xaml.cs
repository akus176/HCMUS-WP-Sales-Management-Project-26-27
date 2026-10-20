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
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
        _ = ((MainWindow)_window).ConnectToDatabaseAsync();
    }

    internal static void ConfigureServices(string connectionString)
    {
        var services = new ServiceCollection()
            .AddCoffeeShopDirectClient(connectionString)
            .BuildServiceProvider();

        var previousServices = Services;
        Services = services;
        previousServices?.Dispose();
    }
}
