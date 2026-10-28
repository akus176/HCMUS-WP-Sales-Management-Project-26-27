using System.Xml.Linq;

namespace CoffeeShop.WinUI.Tests;

public sealed class StockRoomXamlTests
{
    [Fact]
    public void ExplicitStyles_TargetTheNativeControlReceivingThem()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var root = Path.Combine(AppContext.BaseDirectory, "Xaml");
        var page = XDocument.Load(Path.Combine(root, "StockRoomPage.xaml"));
        var styles = Directory.GetFiles(Path.Combine(root, "Theme"), "*.xaml")
            .SelectMany(path => XDocument.Load(path).Descendants())
            .Concat(page.Descendants())
            .Where(element => element.Name.LocalName == "Style" && element.Attribute(xaml + "Key") is not null)
            .ToDictionary(element => (string)element.Attribute(xaml + "Key")!,
                element => (string)element.Attribute("TargetType")!);

        foreach (var element in page.Descendants().Where(element => element.Attribute("Style") is not null))
        {
            var reference = (string)element.Attribute("Style")!;
            const string prefix = "{StaticResource ";
            Assert.StartsWith(prefix, reference);
            var key = reference[prefix.Length..^1];
            Assert.True(styles.TryGetValue(key, out var target), $"Missing style {key}.");
            Assert.True(target == element.Name.LocalName,
                $"{(string?)element.Attribute(xaml + "Name") ?? element.Name.LocalName}: {key} targets {target}, not {element.Name.LocalName}.");
        }
    }
}
