#if !COFFEESHOP_API_ONLY
using CoffeeShop.Infrastructure.Postgres;
#endif
using CoffeeShop.WinUI.Pages;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly ServiceConsoleConnection connection = new();
    private readonly Dictionary<string, Page> pages = [];
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(30) };
    private string? currentRoute;
    private ServiceConsoleLayoutMode? layout;
    private bool selecting;
    private bool connecting;
    private bool checkingSession;
    private bool endingSession;
    private readonly PosOperatorSession staffSession;
    private readonly StaffSessionManager? sessionManager;
    private readonly bool verifyShell = Environment.GetCommandLineArgs().Contains("--verify-shell");
    private bool UsePermissionNavigation => sessionManager is not null && !verifyShell;

    public MainWindow()
    {
        InitializeComponent();
#if COFFEESHOP_API_ONLY
        ClientModeText.Text = "WINUI · HTTP API";
#else
        ClientModeText.Text = App.IsApiMode ? "WINUI · HTTP API" : "WINUI · DIRECT POSTGRESQL";
#endif
        staffSession = App.Services.GetRequiredService<PosOperatorSession>();
        sessionManager = App.Services.GetService<StaffSessionManager>();
        SignOutButton.Visibility = sessionManager is null ? Visibility.Collapsed : Visibility.Visible;
        if (sessionManager is not null) sessionManager.SessionExpired += OnSessionExpired;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleDragRegion);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 243, 238, 228);
        AppWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 198, 189, 176);
        AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(1440, 900));
        InitializeNavigation();
        UpdateClock();
        clock.Tick += async (_, _) =>
        {
            UpdateClock();
            await CheckSessionAsync();
        };
        clock.Start();
        Closed += (_, _) =>
        {
            clock.Stop();
            if (sessionManager is not null) sessionManager.SessionExpired -= OnSessionExpired;
        };
    }
    private static object Resource(string key) => Microsoft.UI.Xaml.Application.Current.Resources[key];
    private static Symbol NavigationSymbol(string tag) => tag switch
    {
        "counter" => Symbol.Shop,
        "service-board" => Symbol.ViewAll,
        "menu" => Symbol.Bullets,
        "stock-room" => Symbol.Library,
        "customers" => Symbol.People,
        _ => Symbol.ShowResults,
    };
    private void InitializeNavigation()
    {
        string? group = null;
        var destinations = UsePermissionNavigation
            ? ServiceConsoleNavigation.ForPermissions(staffSession.Permissions)
            : ServiceConsoleNavigation.PrimaryDestinations;
        foreach (var destination in destinations)
        {
            if (group != destination.Group)
            {
                group = destination.Group;
                var header = new NavigationViewItemHeader
                {
                    Content = group,
                    Style = (Style)Resource("ConsoleNavigationGroupStyle"),
                    FontSize = 11,
                    CharacterSpacing = 0,
                    Height = double.NaN,
                    MinHeight = 28
                };
                if (Nav.MenuItems.Count == 0) header.Margin = new Thickness(18, 24, 18, 3);
                Nav.MenuItems.Add(header);
            }
            var item = new NavigationViewItem
            {
                Content = destination.Label,
                Icon = new SymbolIcon(NavigationSymbol(destination.Tag)),
                Tag = destination.Tag,
                Style = (Style)Resource("ConsoleNavigationItemStyle"),
                FontSize = 14,
                CharacterSpacing = 0
            };
            AutomationProperties.SetName(item, $"Open {destination.Label} workspace");
            ToolTipService.SetToolTip(item, destination.Label);
            Nav.MenuItems.Add(item);
        }
    }
    private void OnPaneChanged(NavigationView sender, object args) => RefreshNavigationItems();
    private void RefreshNavigationItems()
    {
        if (BrandCopy is null || SidebarFooter is null) return;
        var open = layout == ServiceConsoleLayoutMode.Expanded || Nav.IsPaneOpen;
        foreach (var item in Nav.MenuItems.OfType<NavigationViewItem>())
        {
            item.Margin = new Thickness(open ? 18 : 8, 0, open ? 18 : 8, 0);
        }
        foreach (var header in Nav.MenuItems.OfType<NavigationViewItemHeader>()) header.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        BrandCopy.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        SidebarFooter.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        SidebarRule.Visibility = layout == ServiceConsoleLayoutMode.Compact && !open ? Visibility.Collapsed : Visibility.Visible;
        SidebarRule.Margin = new Thickness(open ? 215 : 63, 0, 0, 0);
    }
    private void UpdateClock()
    {
        ClockText.Text = DateTime.Now.ToString("HH:mm");
        DateText.Text = DateTime.Now.ToString("ddd dd MMM", System.Globalization.CultureInfo.GetCultureInfo("en-US")).ToUpperInvariant();
    }
    public async Task ConnectToDatabaseAsync()
    {
        if (connecting) return;
        connecting = true;
        connection.Begin();
        PresentConnection();
        try
        {
            var configuration = CoffeeShopClientConfiguration.Load();
            if (configuration.IsApiMode)
            {
                connection.Connected();
                PresentConnection();
                if (currentRoute is null && Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault() is { } apiFirstItem)
                    NavigateTo((string)apiFirstItem.Tag);
                if (verifyShell) await VerifyShellAsync();
                return;
            }
#if !COFFEESHOP_API_ONLY
            if (!configuration.IsConfigured) { Fail(configuration.Error ?? "Database settings are missing."); return; }
            using var scope = App.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            if (!await database.Database.CanConnectAsync()) { Fail("PostgreSQL is unavailable. Check the server and database settings, then retry."); return; }
            connection.Connected();
            PresentConnection();
            if (currentRoute is null && Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault() is { } firstItem)
                NavigateTo((string)firstItem.Tag);
            if (Environment.GetCommandLineArgs().Contains("--verify-shell")) await VerifyShellAsync();
#else
            Fail("The API-only Release client requires a valid API configuration.");
#endif
        }
        catch (Exception)
        {
#if COFFEESHOP_API_ONLY
            Fail("Could not connect to the coffee shop API. Check the server address and retry.");
#else
            Fail("Could not connect to PostgreSQL. Check the server and database settings, then retry.");
#endif
        }
        finally { connecting = false; }
    }
    private void Fail(string message) { connection.Failed(message); PresentConnection(); }
    private void PresentConnection()
    {
        Nav.IsEnabled = connection.IsConnected;
        ConnectionStatusText.Text = connection.Status;
        DatabaseFailurePanel.Visibility = connection.Failure is null ? Visibility.Collapsed : Visibility.Visible;
        DatabaseStatusText.Text = connection.Failure ?? "Checking database connection…";
        RetryDatabaseButton.IsEnabled = !connection.IsChecking;
    }
    private async void OnRetryDatabaseClick(object sender, RoutedEventArgs e) => await ConnectToDatabaseAsync();
    private void OnTogglePane(object sender, RoutedEventArgs e) => Nav.IsPaneOpen = !Nav.IsPaneOpen;
    public void NavigateTo(string tag)
    {
        if (!connection.IsConnected) return;
        var route = UsePermissionNavigation
            ? ServiceConsoleNavigation.Resolve(tag, staffSession.Permissions)
            : ServiceConsoleNavigation.Resolve(tag);
        selecting = true;
        Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>().Single(item => Equals(item.Tag, route.Destination.Tag));
        selecting = false;
        ShowRoute(route);
    }
    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (!selecting && args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            var route = UsePermissionNavigation
                ? ServiceConsoleNavigation.Resolve(tag, staffSession.Permissions)
                : ServiceConsoleNavigation.Resolve(tag);
            ShowRoute(route, tag == "stock-room");
        }
    }
    private async void OnSignOut(object sender, RoutedEventArgs e)
    {
        if (sessionManager is null || endingSession) return;
        endingSession = true;
        try { await sessionManager.LogoutAsync(); }
        catch (ApiException) { /* Local credentials are cleared even when the API is offline. */ }
        catch (OperationCanceledException) { /* A timeout must not leave the console open. */ }
        ReturnToSignIn();
    }
    private async Task CheckSessionAsync()
    {
        if (sessionManager is null || verifyShell || checkingSession || endingSession) return;
        checkingSession = true;
        try { await sessionManager.GetValidAccessTokenAsync(); }
        catch (ApiException) { /* A temporary network failure is not session revocation. */ }
        catch (OperationCanceledException) { /* A timeout is not session revocation. */ }
        finally { checkingSession = false; }
    }
    private void OnSessionExpired() => DispatcherQueue.TryEnqueue(() =>
    {
        if (endingSession) return;
        endingSession = true;
        ReturnToSignIn();
    });
    private void ReturnToSignIn()
    {
        ContentFrame.Content = null;
        pages.Clear();
        staffSession.Operator = null;
        ((App)Microsoft.UI.Xaml.Application.Current).ShowLoginWindow();
        Close();
    }
    private void ShowRoute(ServiceConsoleRoute route, bool preserveStockSection = false)
    {
        if (route.Destination.Workspace == ServiceConsoleWorkspace.StockRoom)
        {
            const string stockKey = "stock-room";
            if (!pages.TryGetValue(stockKey, out var stockPage))
            {
                pages.Add(stockKey, stockPage = new StockRoomPage());
            }
            if (currentRoute != stockKey)
            {
                ContentFrame.Content = stockPage;
                currentRoute = stockKey;
            }
            if (!preserveStockSection && stockPage is StockRoomPage page)
            {
                page.SelectSection(route.StockSection ?? "ingredients");
            }
        }
        else
        {
            var key = route.Destination.Tag;
            if (currentRoute != key)
            {
                if (!pages.TryGetValue(key, out var page)) pages.Add(key, page = CreatePage(route));
                ContentFrame.Content = page;
                currentRoute = key;
            }
        }

        BreadcrumbGroup.Text = route.Destination.Group.ToUpperInvariant();
        BreadcrumbText.Text = route.Destination.Label.ToUpperInvariant();
        if (layout != ServiceConsoleLayoutMode.Expanded) Nav.IsPaneOpen = false;
    }
    private static Page CreatePage(ServiceConsoleRoute route) => route.Destination.Workspace switch
    {
        ServiceConsoleWorkspace.Counter => new PosPage(),
        ServiceConsoleWorkspace.ServiceBoard => new OperationsPage(),
        ServiceConsoleWorkspace.Menu => new CatalogPage(),
        ServiceConsoleWorkspace.StockRoom => new StockRoomPage(),
        ServiceConsoleWorkspace.Customers => new CustomersPage(),
        ServiceConsoleWorkspace.Reports => new ReportsPage(),
        _ => throw new ArgumentOutOfRangeException(nameof(route)),
    };
    private void OnShellSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Nav is null) return;
        var next = ServiceConsoleNavigation.LayoutFor(e.NewSize.Width);
        if (layout == next) return;
        layout = next;
        Nav.PaneDisplayMode = next switch
        {
            ServiceConsoleLayoutMode.Expanded => NavigationViewPaneDisplayMode.Left,
            ServiceConsoleLayoutMode.Medium => NavigationViewPaneDisplayMode.LeftCompact,
            _ => NavigationViewPaneDisplayMode.LeftMinimal,
        };
        Nav.IsPaneOpen = next == ServiceConsoleLayoutMode.Expanded;
        BrandCopy.Visibility = SidebarFooter.Visibility = next == ServiceConsoleLayoutMode.Expanded ? Visibility.Visible : Visibility.Collapsed;
        DateText.Visibility = next == ServiceConsoleLayoutMode.Compact ? Visibility.Collapsed : Visibility.Visible;
        MenuToggle.Visibility = next == ServiceConsoleLayoutMode.Expanded ? Visibility.Collapsed : Visibility.Visible;
        RefreshNavigationItems();
        DatabaseFailurePanel.Margin = new Thickness(next == ServiceConsoleLayoutMode.Expanded ? 248 : 20, 60, 20, 20);
    }
}
