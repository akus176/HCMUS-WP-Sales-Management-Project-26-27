using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.WinUI.Pages;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CoffeeShop.WinUI;

public sealed partial class MainWindow : Window
{
    private ServiceConsoleLayoutMode? _layoutMode;

    public MainWindow()
    {
        InitializeComponent();
        InitializeNavigation();
        ApplyConnectionPresentation(ServiceConsoleConnectionPresentation.Checking());
    }

    public async Task ConnectToDatabaseAsync()
    {
        ApplyConnectionPresentation(ServiceConsoleConnectionPresentation.Checking());

        var configuration = CoffeeShopClientConfiguration.Load();
        if (!configuration.IsConfigured)
        {
            ShowDatabaseFailure(configuration.Error ?? "Database connection settings are missing.");
            return;
        }

        try
        {
            App.ConfigureServices(configuration.ConnectionString!);
            using var scope = App.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            if (!await database.Database.CanConnectAsync())
            {
                ShowDatabaseFailure("PostgreSQL did not accept the connection. Confirm the server is running and the configured database credentials are correct.");
                return;
            }

            ApplyConnectionPresentation(ServiceConsoleConnectionPresentation.Connected(configuration.Source ?? "configuration"));
            if (Nav.SelectedItem is null)
            {
                NavigateTo(ServiceConsoleNavigation.DefaultTag);
            }
        }
        catch (Exception)
        {
            ShowDatabaseFailure("Could not connect to PostgreSQL. Confirm the server is running and the configured database name and credentials are correct.");
        }
    }

    private void InitializeNavigation()
    {
        var itemStyle = (Style)Microsoft.UI.Xaml.Application.Current.Resources["ServiceConsoleNavigationItemStyle"];
        foreach (var destination in ServiceConsoleNavigation.PrimaryDestinations)
        {
            var item = new NavigationViewItem
            {
                Content = destination.Label,
                Tag = destination.Tag,
                Icon = new FontIcon { Glyph = destination.Glyph },
                Style = itemStyle,
            };
            AutomationProperties.SetName(item, destination.AccessibleName);
            Nav.MenuItems.Add(item);
        }
    }

    private void ShowDatabaseFailure(string message) =>
        ApplyConnectionPresentation(ServiceConsoleConnectionPresentation.Failed(message));

    private void ApplyConnectionPresentation(ServiceConsoleConnectionPresentation presentation)
    {
        Nav.IsEnabled = presentation.IsNavigationEnabled;
        DatabaseFailurePanel.Visibility = presentation.ShowBlockingFailure ? Visibility.Visible : Visibility.Collapsed;
        RetryDatabaseButton.Visibility = presentation.ShowRetry ? Visibility.Visible : Visibility.Collapsed;
        DatabaseStatusText.Text = presentation.DetailText;
        ConnectionStatusText.Text = presentation.StatusText;
        ConnectionStatusDetail.Text = presentation.StatusDetail;
        ConnectionStatusDot.Fill = (Brush)Microsoft.UI.Xaml.Application.Current.Resources[presentation.StatusBrushResourceKey];
    }

    private async void OnRetryDatabaseClick(object sender, RoutedEventArgs e)
    {
        await ConnectToDatabaseAsync();
    }

    private void OnShellSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var layoutMode = ServiceConsoleLayout.ForWidth(e.NewSize.Width);
        if (_layoutMode == layoutMode)
        {
            return;
        }

        _layoutMode = layoutMode;
        switch (layoutMode)
        {
            case ServiceConsoleLayoutMode.Compact:
                Nav.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;
                Nav.IsPaneOpen = false;
                Nav.OpenPaneLength = 220;
                BrandCopy.Visibility = Visibility.Collapsed;
                ShellTitle.Visibility = Visibility.Collapsed;
                ConnectionStatusDetail.Visibility = Visibility.Collapsed;
                DatabaseFailurePanel.Margin = new Thickness(8);
                break;
            case ServiceConsoleLayoutMode.Medium:
                Nav.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftCompact;
                Nav.IsPaneOpen = false;
                Nav.OpenPaneLength = 220;
                BrandCopy.Visibility = Visibility.Collapsed;
                ShellTitle.Visibility = Visibility.Visible;
                ConnectionStatusDetail.Visibility = Visibility.Collapsed;
                DatabaseFailurePanel.Margin = new Thickness(12);
                break;
            case ServiceConsoleLayoutMode.Expanded:
                Nav.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
                Nav.IsPaneOpen = true;
                Nav.OpenPaneLength = 226;
                BrandCopy.Visibility = Visibility.Visible;
                ShellTitle.Visibility = Visibility.Visible;
                ConnectionStatusDetail.Visibility = Visibility.Visible;
                DatabaseFailurePanel.Margin = new Thickness(16);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(layoutMode));
        }
    }

    public void NavigateTo(string tag)
    {
        var destination = ServiceConsoleNavigation.Resolve(tag);
        Nav.SelectedItem = Nav.MenuItems
            .OfType<NavigationViewItem>()
            .Single(item => string.Equals(item.Tag as string, destination.Tag, StringComparison.Ordinal));
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        var destination = ServiceConsoleNavigation.Resolve(tag);
        var pageType = destination.Workspace switch
        {
            ServiceConsoleWorkspace.Counter => typeof(PosPage),
            ServiceConsoleWorkspace.ServiceBoard => typeof(OperationsPage),
            ServiceConsoleWorkspace.Menu => typeof(CatalogPage),
            ServiceConsoleWorkspace.StockRoom => typeof(IngredientsPage),
            ServiceConsoleWorkspace.Customers => typeof(CustomersPage),
            ServiceConsoleWorkspace.Reports => typeof(ReportsPage),
            _ => throw new ArgumentOutOfRangeException(nameof(destination.Workspace)),
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
