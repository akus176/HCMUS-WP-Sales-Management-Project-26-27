using System.Xml.Linq;

namespace CoffeeShop.WinUI.Tests;

public sealed class LoginWindowXamlTests
{
    [Fact]
    public void LoginSurfaceUsesLightThemeAndConsoleInk()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var window = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Xaml", "LoginWindow.xaml"));
        var surface = window.Root!.Element(presentation + "Grid");

        Assert.NotNull(surface);
        Assert.Equal("{StaticResource ConsoleGroundBrush}", (string?)surface.Attribute("Background"));
        Assert.Equal("Light", (string?)surface.Attribute("RequestedTheme"));
    }
}
