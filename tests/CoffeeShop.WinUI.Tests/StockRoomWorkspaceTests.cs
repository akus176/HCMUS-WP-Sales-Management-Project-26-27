using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.Tests;

public sealed class StockRoomWorkspaceTests
{
    [Theory]
    [InlineData("ingredients", "stock-room", "ingredients")]
    [InlineData("lowstock", "stock-room", "lowstock")]
    [InlineData("receipts", "stock-room", "receipts")]
    [InlineData("suppliers", "stock-room", "suppliers")]
    [InlineData("adjustments", "stock-room", "adjustments")]
    [InlineData("movements", "stock-room", "movements")]
    public void StockRoom_PreservesLegacyRoutesUnderConsolidatedHost(string legacyTag, string expectedDest, string expectedSection)
    {
        var route = ServiceConsoleNavigation.Resolve(legacyTag);
        Assert.Equal(expectedDest, route.Destination.Tag);
        Assert.Equal(expectedSection, route.StockSection);
        Assert.Equal(ServiceConsoleWorkspace.StockRoom, route.Destination.Workspace);
    }

    [Fact]
    public void StockRoom_DefinesAllSixCoreSections()
    {
        Assert.Equal(6, ServiceConsoleNavigation.StockSections.Count);
        Assert.Equal("Balances", ServiceConsoleNavigation.StockSections["ingredients"]);
        Assert.Equal("Low stock", ServiceConsoleNavigation.StockSections["lowstock"]);
        Assert.Equal("Receipts", ServiceConsoleNavigation.StockSections["receipts"]);
        Assert.Equal("Suppliers", ServiceConsoleNavigation.StockSections["suppliers"]);
        Assert.Equal("Adjustments", ServiceConsoleNavigation.StockSections["adjustments"]);
        Assert.Equal("Movements", ServiceConsoleNavigation.StockSections["movements"]);
    }

    [Theory]
    [InlineData(400, ServiceConsoleLayoutMode.Compact)]
    [InlineData(640, ServiceConsoleLayoutMode.Compact)]
    [InlineData(641, ServiceConsoleLayoutMode.Medium)]
    [InlineData(1000, ServiceConsoleLayoutMode.Medium)]
    [InlineData(1008, ServiceConsoleLayoutMode.Expanded)]
    [InlineData(1440, ServiceConsoleLayoutMode.Expanded)]
    public void StockRoom_SupportsResponsiveBreakpointReflow(double width, ServiceConsoleLayoutMode expected)
    {
        Assert.Equal(expected, ServiceConsoleNavigation.LayoutFor(width));
    }

    [Fact]
    public void StockRoom_InventoryAuditRequiresIngredientSelection()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var model = new InventoryAuditWorkspace(provider.GetRequiredService<IServiceScopeFactory>(), new());

        Assert.False(model.CanAdjust);
        Assert.Null(model.SelectedIngredientId);
        Assert.Equal("Choose an ingredient to inspect its balance and latest 100 movements.", model.Message);
    }
}
