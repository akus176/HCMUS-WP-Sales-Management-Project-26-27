using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CoffeeShop.WinUI.Controls;

/// <summary>Tiles the actual concept SVG without adding focus or hit-test targets.</summary>
public sealed class ConceptPattern : Canvas
{
    private (int Columns, int Rows) previousSize;
    public ConceptPattern()
    {
        IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        SizeChanged += (_, _) => Render();
    }
    private void Render()
    {
        var size = ((int)Math.Ceiling(ActualWidth / 24), (int)Math.Ceiling(ActualHeight / 24));
        Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, ActualWidth, ActualHeight) };
        if (previousSize == size) return;
        previousSize = size;
        Children.Clear();
        var source = new SvgImageSource(new Uri("ms-appx:///Assets/Patterns/lattice.svg"));
        for (var y = 0; y < size.Item2; y++)
            for (var x = 0; x < size.Item1; x++)
            {
                var image = new Image { Source = source, Width = 24, Height = 24 };
                AutomationProperties.SetAccessibilityView(image, AccessibilityView.Raw);
                SetLeft(image, x * 24); SetTop(image, y * 24); Children.Add(image);
            }
    }
}
