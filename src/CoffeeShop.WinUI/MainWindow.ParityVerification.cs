using System.Text.Json;
using CoffeeShop.WinUI.Pages;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CoffeeShop.WinUI;

public sealed partial class MainWindow
{
    // Opt-in integration evidence. Reads the configured database and exercises
    // local presentation state; never submits a business command.
    private async Task VerifyParityAsync()
    {
        var directory = Environment.GetEnvironmentVariable("COFFEESHOP_UI_EVIDENCE")
            ?? PresentationVerification.DirectoryPath;
        Directory.CreateDirectory(directory);
        var results = new List<object>();
        var smoke = new List<object>();
        var defaultIsCounter = currentRoute == ServiceConsoleNavigation.DefaultTag;
        try
        {
            foreach (var destination in ServiceConsoleNavigation.PrimaryDestinations)
            {
                NavigateTo(destination.Tag);
                await Task.Delay(300);
                var page = (Page)ContentFrame.Content;
                var fixture = page switch
                {
                    PosPage counter => await counter.PrepareVerificationAsync(),
                    OperationsPage board => await board.PrepareVerificationAsync(),
                    CatalogPage menu => await menu.PrepareVerificationAsync(),
                    StockRoomPage stock => await stock.PrepareVerificationAsync(),
                    CustomersPage customers => await customers.PrepareVerificationAsync(),
                    ReportsPage reports => await reports.PrepareVerificationAsync(),
                    _ => throw new InvalidOperationException("Unexpected workspace.")
                };
                if (page is PosPage populatedCounter && !populatedCounter.PrepareTicketCapture())
                    throw new InvalidOperationException("Counter capture requires a fresh seeded ticket.");
                object State() => page switch
                {
                    PosPage counter => counter.VerificationState(),
                    OperationsPage board => board.VerificationState(),
                    CatalogPage menu => menu.VerificationState(),
                    StockRoomPage stock => stock.VerificationState(),
                    CustomersPage customers => customers.VerificationState(),
                    ReportsPage reports => reports.VerificationState(),
                    _ => throw new InvalidOperationException("Unexpected workspace.")
                };
                var initialState = JsonSerializer.Serialize(State());
                var capturedFocus = false;
                foreach (var width in new[] { 1440, 1008, 1007, 641, 640, 600, 1440 })
                {
                    await ResizeParityViewportAsync(width);
                    var buttons = PresentationVerification.Elements(page).OfType<Button>()
                        .Where(button => IsVisible(button) && button.ActualWidth > 0 && button.ActualHeight > 0).ToArray();
                    var primaryStyle = (Style)Resource("ConsolePrimaryButtonStyle");
                    bool IsPrimary(Button button)
                    {
                        for (var style = button.Style; style is not null; style = style.BasedOn)
                            if (ReferenceEquals(style, primaryStyle)) return true;
                        return false;
                    }
                    var primaryButtons = buttons.Where(IsPrimary).ToArray();
                    var focusTarget = primaryButtons.FirstOrDefault(button => button.IsEnabled)
                        ?? buttons.FirstOrDefault(button => button.IsEnabled);
                    var keyboardFocus = focusTarget?.Focus(FocusState.Keyboard) == true;
                    if (width == 1440 && !capturedFocus)
                    {
                        await PresentationVerification.CaptureAsync(ShellRoot, $"native-{destination.Tag}-focus.png");
                        capturedFocus = true;
                    }
                    var targets = buttons.Select(button => new
                    {
                        name = FrameworkElementAutomationPeer.CreatePeerForElement(button)?.GetName() ?? AutomationProperties.GetName(button),
                        width = button.ActualWidth,
                        height = button.ActualHeight,
                        primary = IsPrimary(button),
                        enabled = button.IsEnabled
                    }).ToArray();
                    var horizontalOverflow = PresentationVerification.Elements(page).OfType<ScrollViewer>()
                        .Where(scroll => IsVisible(scroll) && scroll.ScrollableWidth > 1)
                        .Select(scroll => new { scroll.Name, scroll.ScrollableWidth, scroll.HorizontalScrollMode, pageLevel = ReferenceEquals(scroll, page.Content) }).ToArray();
                    var navigation = Nav.MenuItems.OfType<NavigationViewItem>().Select(item => new
                    {
                        tag = item.Tag,
                        name = AutomationProperties.GetName(item),
                        height = item.ActualHeight,
                        contentInset = NavigationContentInset(item)
                    }).ToArray();
                    results.Add(new
                    {
                        tag = destination.Tag,
                        width,
                        height = 900,
                        fixture,
                        state = State(),
                        samePage = ReferenceEquals(page, ContentFrame.Content),
                        statePreserved = initialState == JsonSerializer.Serialize(State()),
                        mode = layout.ToString(),
                        actualWidth = ShellRoot.ActualWidth,
                        actualHeight = ShellRoot.ActualHeight,
                        keyboardFocus,
                        targets,
                        navigation,
                        horizontalOverflow,
                        primaryTargetsPass = primaryButtons.Length > 0 && primaryButtons.All(button => button.ActualWidth >= 48 && button.ActualHeight >= 48),
                        namesPass = targets.All(target => !string.IsNullOrWhiteSpace(target.name)),
                        layout = page switch
                        {
                            PosPage counter => counter.VerificationLayout(),
                            CatalogPage menu => menu.VerificationLayout(),
                            CustomersPage customers => customers.VerificationLayout(),
                            ReportsPage reports => reports.VerificationLayout(),
                            OperationsPage board => board.VerificationLayout(),
                            _ => (object?)null
                        }
                    });
                    focusTarget?.Focus(FocusState.Programmatic);
                    if (width is 1440 or 600)
                        await PresentationVerification.CaptureAsync(ShellRoot, $"native-{destination.Tag}-{width}.png");
                    ShellRoot.Width = ShellRoot.Height = double.NaN;
                }
                if (page is PosPage capturedCounter) capturedCounter.ClearTicketCapture();
            }
            foreach (var width in new[] { 1440, 600 })
            {
                foreach (var destination in ServiceConsoleNavigation.PrimaryDestinations)
                {
                    NavigateTo(destination.Tag);
                    await Task.Delay(300);
                    await ResizeParityViewportAsync(width);
                    object checks = ContentFrame.Content switch
                    {
                        PosPage counter => await counter.VerifyInteractionsAsync(),
                        OperationsPage board => await board.VerifyBoardAsync(),
                        CatalogPage menu => await menu.VerifyMenuInteractionsAsync(),
                        StockRoomPage stock => await stock.VerifyInteractionsAsync(),
                        CustomersPage customers => await customers.VerifyCustomerInteractionsAsync(ResizeParityViewportAsync),
                        ReportsPage reports => await reports.VerifyReportInteractionsAsync(),
                        _ => throw new InvalidOperationException("Unexpected workspace.")
                    };
                    if (ContentFrame.Content is OperationsPage verifiedBoard) verifiedBoard.RestoreVerificationOperator();
                    smoke.Add(new { tag = destination.Tag, width, checks });
                }
            }
            NavigateTo("menu");
            await Task.Delay(300);
            var editor = await ((CatalogPage)ContentFrame.Content).VerifyEditorAsync(ResizeParityViewportAsync);
            await File.WriteAllTextAsync(Path.Combine(directory, "parity-verification.json"), JsonSerializer.Serialize(new
            {
                defaultIsCounter,
                connected = connection.IsConnected,
                destinations = ServiceConsoleNavigation.PrimaryDestinations.Select(destination => destination.Tag),
                results,
                smoke,
                editor
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception)
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "parity-error.txt"), exception.ToString());
        }
        finally
        {
            ShellRoot.Width = ShellRoot.Height = double.NaN;
            if (Environment.GetCommandLineArgs().Contains("--verify-exit")) Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }

    private async Task ResizeParityViewportAsync(int width)
    {
        await ResizeForVerificationAsync(width, 900);
        ShellRoot.Width = width;
        ShellRoot.Height = 900;
        ShellRoot.UpdateLayout();
        await Task.Delay(200);
    }

    private static bool IsVisible(DependencyObject element)
    {
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement { Visibility: Visibility.Collapsed }) return false;
        return true;
    }

}
