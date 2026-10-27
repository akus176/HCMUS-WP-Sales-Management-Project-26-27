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
        if (Environment.GetCommandLineArgs().Contains("--components"))
        {
            _window = new Window { Title = "Corner Cafe · Component gallery", Content = new Pages.ComponentGalleryPage() };
            _window.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(1440, 900));
            _window.Activate();
            _window.AppWindow.Show();
            return;
        }
        _window = new MainWindow();
        _window.Activate();
        if (Environment.GetCommandLineArgs().Contains("--verify-shell")) _window.AppWindow.Show();
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
