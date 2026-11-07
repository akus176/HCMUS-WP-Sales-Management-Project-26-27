using CoffeeShop.WinUI.Services;
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
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (Environment.GetCommandLineArgs().Contains("--components"))
        {
            _window = new Window { Title = "Corner Cafe · Component gallery", Content = new Pages.ComponentGalleryPage() };
            _window.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(1440, 900));
            _window.Activate();
            _window.AppWindow.Show();
            return;
        }
        var configuration = CoffeeShopClientConfiguration.Load();
        ConfigureServices(configuration);
        if (!Environment.GetCommandLineArgs().Contains("--verify-shell"))
        {
            try
            {
                var session = Services.GetRequiredService<StaffSessionManager>();
                if (await session.RestoreAsync() is null)
                {
                    ShowLoginWindow();
                    return;
                }
            }
            catch (ApiException)
            {
                ShowLoginWindow();
                return;
            }
        }
        ShowMainWindow();
    }

    internal void ShowMainWindow()
    {
        _window = new MainWindow();
        _window.Activate();
        if (Environment.GetCommandLineArgs().Contains("--verify-shell")) _window.AppWindow.Show();
        _ = ((MainWindow)_window).ConnectToDatabaseAsync();
    }

    internal void ShowLoginWindow()
    {
        _window = new LoginWindow();
        _window.Activate();
    }

    private static void ConfigureServices(CoffeeShopClientConfiguration configuration)
    {
        var registrations = new ServiceCollection()
            .AddCoffeeShopApiClient(configuration.ApiBaseAddress)
            .AddCoffeeShopDirectClient(configuration.ConnectionString ?? string.Empty);
        if (!Environment.GetCommandLineArgs().Contains("--verify-shell"))
        {
            registrations.AddSingleton(provider => new PosWorkspace(provider.GetRequiredService<IServiceScopeFactory>(),
                provider.GetRequiredService<CustomerOrderContext>(), provider.GetRequiredService<PosOperatorSession>(),
                provider.GetRequiredService<ApiPosSource>()));
            registrations.AddSingleton(provider => new OperationsWorkspace(provider.GetRequiredService<IServiceScopeFactory>(),
                provider.GetRequiredService<PosOperatorSession>(), provider.GetRequiredService<ApiOperationsSource>()));
        }
        var services = registrations.BuildServiceProvider();

        var previousServices = Services;
        Services = services;
        previousServices?.Dispose();
    }
}
