using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.WinUI.Pages;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

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
    private readonly StaffSessionManager sessionManager;
    private readonly bool verifyShell = Environment.GetCommandLineArgs().Contains("--verify-shell");

    public MainWindow()
    {
        InitializeComponent();
        staffSession = App.Services.GetRequiredService<PosOperatorSession>();
        sessionManager = App.Services.GetRequiredService<StaffSessionManager>();
        sessionManager.SessionExpired += OnSessionExpired;
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
            sessionManager.SessionExpired -= OnSessionExpired;
        };
    }
    private static object Resource(string key) => Microsoft.UI.Xaml.Application.Current.Resources[key];
    private static string IconName(string tag) => tag switch
    {
        "counter" => "i-counter",
        "service-board" => "i-board",
        "menu" => "i-menu",
        "stock-room" => "i-stock",
        "customers" => "i-customers",
        _ => "i-reports",
    };
    private void InitializeNavigation()
    {
        string? group = null;
        var destinations = verifyShell
            ? ServiceConsoleNavigation.PrimaryDestinations
            : ServiceConsoleNavigation.ForPermissions(staffSession.Permissions);
        foreach (var destination in destinations)
        {
            if (group != destination.Group)
            {
                group = destination.Group;
                var header = new NavigationViewItemHeader { Content = group.ToUpperInvariant(), Style = (Style)Resource("ConsoleNavigationGroupStyle") };
                if (Nav.MenuItems.Count == 0) header.Margin = new Thickness(18, 24, 18, 3);
                Nav.MenuItems.Add(header);
            }
            var item = new NavigationViewItem { Content = NavigationContent(destination), Tag = destination.Tag, Style = (Style)Resource("ConsoleNavigationItemStyle") };
            AutomationProperties.SetName(item, $"Open {destination.Label} workspace");
            ToolTipService.SetToolTip(item, destination.Label);
            Nav.MenuItems.Add(item);
        }
    }
    private static StackPanel NavigationContent(ServiceConsoleDestination destination)
    {
        // Keep the approved inset inside the selected navigation surface.
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(4, 0, 4, 0) };
        content.Children.Add(new TextBlock { Text = destination.Numeral, Width = 17, FontSize = 13, FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Resource("ConsoleSerifFont") });
        var image = new Image { Width = 13, Height = 13, Source = new SvgImageSource(new Uri($"ms-appx:///Assets/Icons/{IconName(destination.Tag)}.svg")) };
        AutomationProperties.SetAccessibilityView(image, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        content.Children.Add(image);
        content.Children.Add(new TextBlock { Text = destination.Label.ToUpperInvariant(), FontSize = 9, CharacterSpacing = 140, VerticalAlignment = VerticalAlignment.Center });
        return content;
    }
    private void OnPaneChanged(NavigationView sender, object args) => RefreshNavigationItems();
    private void RefreshNavigationItems()
    {
        if (BrandCopy is null || SidebarFooter is null) return;
        var open = layout == ServiceConsoleLayoutMode.Expanded || Nav.IsPaneOpen;
        foreach (var item in Nav.MenuItems.OfType<NavigationViewItem>())
        {
            var destination = ServiceConsoleNavigation.Resolve((string)item.Tag).Destination;
            item.Icon = open ? null : new ImageIcon { Source = new SvgImageSource(new Uri($"ms-appx:///Assets/Icons/{IconName(destination.Tag)}.svg")) };
            item.Content = open ? NavigationContent(destination) : destination.Label;
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
            if (!configuration.IsConfigured) { Fail(configuration.Error ?? "Database settings are missing."); return; }
            using var scope = App.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            if (!await database.Database.CanConnectAsync()) { Fail("PostgreSQL is unavailable. Check the server and database settings, then retry."); return; }
            connection.Connected();
            PresentConnection();
            if (currentRoute is null && Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault() is { } firstItem)
                NavigateTo((string)firstItem.Tag);
            if (Environment.GetCommandLineArgs().Contains("--verify-shell")) await VerifyShellAsync();
        }
        catch (Exception) { Fail("Could not connect to PostgreSQL. Check the server and database settings, then retry."); }
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
        var route = verifyShell
            ? ServiceConsoleNavigation.Resolve(tag)
            : ServiceConsoleNavigation.Resolve(tag, staffSession.Permissions);
        selecting = true;
        Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>().Single(item => Equals(item.Tag, route.Destination.Tag));
        selecting = false;
        ShowRoute(route);
    }
    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (!selecting && args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            var route = verifyShell ? ServiceConsoleNavigation.Resolve(tag) : ServiceConsoleNavigation.Resolve(tag, staffSession.Permissions);
            ShowRoute(route, tag == "stock-room");
        }
    }
    private async void OnSignOut(object sender, RoutedEventArgs e)
    {
        if (endingSession) return;
        endingSession = true;
        try { await sessionManager.LogoutAsync(); }
        catch (ApiException) { /* Local credentials are cleared even when the API is offline. */ }
        ReturnToSignIn();
    }
    private async Task CheckSessionAsync()
    {
        if (verifyShell || checkingSession || endingSession) return;
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
        TitleCaption.Visibility = DateText.Visibility = next == ServiceConsoleLayoutMode.Compact ? Visibility.Collapsed : Visibility.Visible;
        MenuToggle.Visibility = next == ServiceConsoleLayoutMode.Expanded ? Visibility.Collapsed : Visibility.Visible;
        RefreshNavigationItems();
        DatabaseFailurePanel.Margin = new Thickness(next == ServiceConsoleLayoutMode.Expanded ? 248 : 20, 76, 20, 20);
    }
}
