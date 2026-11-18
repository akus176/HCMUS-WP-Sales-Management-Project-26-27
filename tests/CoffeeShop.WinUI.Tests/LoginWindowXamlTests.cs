using System.Xml.Linq;

namespace CoffeeShop.WinUI.Tests;

public sealed class LoginWindowXamlTests
{
    [Fact]
    public void LoginSurfaceUsesConsolePaletteAndSystemContrastColors()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var window = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Xaml", "LoginWindow.xaml"));
        var surface = window.Root!.Element(presentation + "Grid");

        Assert.NotNull(surface);
        Assert.Equal("{StaticResource ConsoleGroundBrush}", (string?)surface.Attribute("Background"));
        Assert.Equal("Light", (string?)surface.Attribute("RequestedTheme"));
        Assert.Equal("{ThemeResource SignInGroundBrush}", (string?)surface.Element(presentation + "Grid")?.Attribute("Background"));

        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var dictionaries = surface.Descendants(presentation + "ResourceDictionary")
            .Where(element => element.Attribute(xaml + "Key") is not null)
            .ToDictionary(element => (string)element.Attribute(xaml + "Key")!);
        var palette = dictionaries["Default"].Elements()
            .ToDictionary(element => (string)element.Attribute(xaml + "Key")!);
        Assert.Equal("ConsoleGroundBrush", (string?)palette["SignInGroundBrush"].Attribute("ResourceKey"));
        Assert.Equal("ConsoleInkBrush", (string?)palette["SignInInkBrush"].Attribute("ResourceKey"));

        var contrast = dictionaries["HighContrast"].Elements()
            .ToDictionary(element => (string)element.Attribute(xaml + "Key")!);
        Assert.Equal("{ThemeResource SystemColorWindowColor}", (string?)contrast["SignInGroundBrush"].Attribute("Color"));
        Assert.Equal("{ThemeResource SystemColorWindowTextColor}", (string?)contrast["SignInInkBrush"].Attribute("Color"));
    }
}
