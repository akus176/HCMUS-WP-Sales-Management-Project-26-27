using System.Text.Json;
using CoffeeShop.WinUI.Pages;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI;

public sealed partial class MainWindow
{
    // Invoked only by --verify-shell. Exercises real routes and native layout,
    // without submitting business commands or writing business data.
    private async Task VerifyShellAsync()
    {
        var results = new List<object>();
        var defaultIsCounter = currentRoute == ServiceConsoleNavigation.DefaultTag;
        foreach (var tag in new[] { "counter", "service-board", "menu", "customers", "reports", "ingredients", "lowstock", "receipts", "suppliers", "adjustments", "movements" })
        {
            NavigateTo(tag);
            await Task.Delay(120);
            results.Add(new
            {
                requested = tag,
                selected = ((NavigationViewItem)Nav.SelectedItem).Tag,
                page = ContentFrame.Content.GetType().Name,
                route = currentRoute,
                stockSection = (ContentFrame.Content as StockRoomPage)?.CurrentSection,
                stockInteraction = (ContentFrame.Content as StockRoomPage)?.VerificationState()
            });
        }
        var verifyStockRoom = Environment.GetCommandLineArgs().Contains("--verify-stock-room");
        var verifyCounter = Environment.GetCommandLineArgs().Contains("--verify-counter");
        var verifyBoard = Environment.GetCommandLineArgs().Contains("--verify-service-board");
        NavigateTo(verifyStockRoom ? "ingredients" : verifyBoard ? "service-board" : "counter");
        if (verifyCounter) await Task.Delay(120); // Allow the cached page's Loaded event to attach its model observer.
        var counterUx = verifyCounter && ContentFrame.Content is PosPage counterPage
            ? await counterPage.VerifyInteractionsAsync() : null;
        var stockUx = verifyStockRoom && ContentFrame.Content is StockRoomPage interactionPage
            ? await interactionPage.VerifyInteractionsAsync() : null;
        if (verifyStockRoom && ContentFrame.Content is StockRoomPage stockRoom) stockRoom.PrepareReferenceCapture();
        var activePage = ContentFrame.Content;
        var resize = new List<object>();
        foreach (var width in new[] { 1440, 1008, 1007, 641, 640, 600, 1440 })
        {
            await ResizeForVerificationAsync(width, 900);
            if (verifyStockRoom && width == 600) await PresentationVerification.CaptureAsync(ShellRoot, "native-stock-room-compact.png");
            if (verifyCounter && width == 600) await PresentationVerification.CaptureAsync(ShellRoot, "native-counter-compact.png");
            if (verifyBoard && width == 600) await PresentationVerification.CaptureAsync(ShellRoot, "native-service-board-compact.png");
            resize.Add(new
            {
                requestedWidth = width,
                actualWidth = ShellRoot.ActualWidth,
                mode = layout.ToString(),
                samePage = ReferenceEquals(activePage, ContentFrame.Content),
                route = currentRoute,
                contentWidth = ContentFrame.ActualWidth,
                boardLayout = verifyBoard ? (ContentFrame.Content as OperationsPage)?.VerificationLayout() : null,
                paneOpen = Nav.IsPaneOpen
            });
        }
        var item = Nav.MenuItems.OfType<NavigationViewItem>().First();
        ShellRoot.Width = 1440;
        ShellRoot.Height = 900;
        ShellRoot.UpdateLayout();
        await Task.Delay(100);
        var boardUx = verifyBoard && ContentFrame.Content is OperationsPage boardPage ? await boardPage.VerifyBoardAsync() : null;
        if (verifyStockRoom && ContentFrame.Content is StockRoomPage referencePage) referencePage.PrepareReferenceCapture();
        var keyboardFocus = !verifyStockRoom && !verifyBoard && item.Focus(FocusState.Keyboard);
        var controls = Nav.MenuItems.OfType<NavigationViewItem>().Select(control => new
        {
            tag = control.Tag,
            name = AutomationProperties.GetName(control),
            width = control.ActualWidth,
            height = control.ActualHeight,
            position = control.TransformToVisual(ShellRoot).TransformPoint(new Windows.Foundation.Point()),
        }).ToArray();
        var allResources = new List<string>();
        foreach (var dictionary in Microsoft.UI.Xaml.Application.Current.Resources.MergedDictionaries)
            foreach (var key in dictionary.Keys) { _ = dictionary[key]; allResources.Add(key.ToString() ?? ""); }
        var report = new
        {
            stockBalanceRendered = !verifyStockRoom || ((StockRoomPage)ContentFrame.Content).HasRenderedBalanceName(),
            stockUx,
            counterUx,
            boardUx,
            defaultIsCounter,
            connected = connection.IsConnected,
            routes = results,
            resize,
            controls,
            keyboardFocus,
            resourcesResolved = allResources.Count,
            viewport = new { width = ShellRoot.ActualWidth, height = ShellRoot.ActualHeight, scale = ShellRoot.XamlRoot.RasterizationScale }
        };
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoffeeShop");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, verifyStockRoom ? "stock-room-verification.json" : "shell-verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        await PresentationVerification.CaptureAsync(ShellRoot, verifyStockRoom ? "native-stock-room.png" : "native-shell.png");
        if (verifyCounter) await PresentationVerification.CaptureAsync(ShellRoot, "native-counter.png");
        if (verifyBoard)
        {
            await PresentationVerification.CaptureAsync(ShellRoot, "native-service-board.png");
            ((OperationsPage)ContentFrame.Content).RestoreVerificationOperator();
        }
        if (verifyCounter && ContentFrame.Content is PosPage servicePage) await servicePage.CaptureServiceFormsAsync();
        if (verifyCounter && ContentFrame.Content is PosPage ticketPage && ticketPage.PrepareTicketCapture())
        {
            try
            {
                await Task.Delay(150);
                await PresentationVerification.CaptureAsync(ShellRoot, "native-counter-populated.png");
                if (ticketPage.FindName("TicketPanel") is FrameworkElement panel)
                    await PresentationVerification.CaptureAsync(panel, "native-counter-ticket.png");
            }
            finally { ticketPage.ClearTicketCapture(); }
        }
        if (verifyStockRoom && ContentFrame.Content is StockRoomPage adjustmentPage)
        {
            adjustmentPage.PrepareAdjustmentCapture();
            await Task.Delay(100);
            await PresentationVerification.CaptureAsync(ShellRoot, "native-stock-room-adjustment.png");
            adjustmentPage.ClearCaptureDraft();
        }
        ShellRoot.Width = ShellRoot.Height = double.NaN;
    }

    private async Task ResizeForVerificationAsync(int width, int height)
    {
        var scale = ShellRoot.XamlRoot.RasterizationScale;
        var presenter = (Microsoft.UI.Windowing.OverlappedPresenter)AppWindow.Presenter;
        if (presenter.State != Microsoft.UI.Windowing.OverlappedPresenterState.Restored) presenter.Restore();
        AppWindow.ResizeClient(new Windows.Graphics.SizeInt32((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale)));
        await Task.Delay(150);
        // AppWindow coordinates and rasterization can differ with display DPI;
        // use observed client dimensions to reach the requested logical viewport.
        if (Math.Abs(ShellRoot.ActualWidth - width) > 1 || Math.Abs(ShellRoot.ActualHeight - height) > 1)
        {
            var adjustedWidth = (int)Math.Ceiling(AppWindow.ClientSize.Width * width / ShellRoot.ActualWidth);
            var adjustedHeight = (int)Math.Ceiling(AppWindow.ClientSize.Height * height / ShellRoot.ActualHeight);
            AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(adjustedWidth, adjustedHeight));
            await Task.Delay(150);
        }
    }
}
