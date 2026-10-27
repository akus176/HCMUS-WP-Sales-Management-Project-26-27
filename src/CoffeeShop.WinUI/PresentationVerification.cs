using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace CoffeeShop.WinUI;

internal static class PresentationVerification
{
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoffeeShop");

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
