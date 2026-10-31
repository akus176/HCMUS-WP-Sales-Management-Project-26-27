using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace CoffeeShop.WinUI;

internal static class PresentationVerification
{
    public static string DirectoryPath
    {
        get
        {
            var output = Environment.GetEnvironmentVariable("COFFEESHOP_VERIFICATION_OUTPUT");
            return string.IsNullOrWhiteSpace(output)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoffeeShop")
                : Path.GetFullPath(output);
        }
    }

    public static IEnumerable<FrameworkElement> Elements(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is FrameworkElement element) yield return element;
            foreach (var descendant in Elements(child)) yield return descendant;
        }
    }

    public static object[] ButtonTargets(DependencyObject root) => Elements(root).OfType<Button>()
        .Where(button => button.Visibility == Visibility.Visible && button.ActualWidth > 0)
        .Select(button => (object)new { name = AutomationProperties.GetName(button) is { Length: > 0 } name ? name : button.Content?.ToString(), width = button.ActualWidth, height = button.ActualHeight }).ToArray();

    // Native XAML capture for deterministic reference-size review, even when a
    // desktop monitor cannot display a complete 900-DIP-high content viewport.
    public static async Task CaptureAsync(FrameworkElement surface, string fileName)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(surface, (int)Math.Round(surface.ActualWidth), (int)Math.Round(surface.ActualHeight));
        var pixels = await bitmap.GetPixelsAsync();
        Directory.CreateDirectory(DirectoryPath);
        var folder = await StorageFolder.GetFolderFromPathAsync(DirectoryPath);
        var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.BitmapTransform.ScaledWidth = (uint)Math.Round(surface.ActualWidth);
        encoder.BitmapTransform.ScaledHeight = (uint)Math.Round(surface.ActualHeight);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }
}
