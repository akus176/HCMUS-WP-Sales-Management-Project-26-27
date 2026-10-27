using System.Text.Json;
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
            results.Add(new { requested = tag, selected = ((NavigationViewItem)Nav.SelectedItem).Tag, page = ContentFrame.Content.GetType().Name, route = currentRoute });
        }
        NavigateTo("counter");
        var activePage = ContentFrame.Content;
        var resize = new List<object>();
        foreach (var width in new[] { 1440, 1008, 1007, 641, 640, 600, 1440 })
        {
            await ResizeForVerificationAsync(width, 900);
            resize.Add(new
            {
                requestedWidth = width,
                actualWidth = ShellRoot.ActualWidth,
                mode = layout.ToString(),
                samePage = ReferenceEquals(activePage, ContentFrame.Content),
                route = currentRoute,
                contentWidth = ContentFrame.ActualWidth,
                paneOpen = Nav.IsPaneOpen
            });
        }
        var item = Nav.MenuItems.OfType<NavigationViewItem>().First();
        ShellRoot.Width = 1440;
        ShellRoot.Height = 900;
        ShellRoot.UpdateLayout();
        await Task.Delay(100);
        var keyboardFocus = item.Focus(FocusState.Keyboard);
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
        await File.WriteAllTextAsync(Path.Combine(directory, "shell-verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        await PresentationVerification.CaptureAsync(ShellRoot, "native-shell.png");
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
