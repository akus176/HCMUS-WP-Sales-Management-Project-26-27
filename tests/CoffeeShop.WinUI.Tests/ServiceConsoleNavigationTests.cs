using CoffeeShop.WinUI.ViewModels;

namespace CoffeeShop.WinUI.Tests;

public sealed class ServiceConsoleNavigationTests
{
    [Theory]
    [InlineData("pos", "counter", null)]
    [InlineData("operations", "service-board", null)]
    [InlineData("catalog", "menu", null)]
    [InlineData("customers", "customers", null)]
    [InlineData("reports", "reports", null)]
    [InlineData("ingredients", "stock-room", "ingredients")]
    [InlineData("lowstock", "stock-room", "lowstock")]
    [InlineData("receipts", "stock-room", "receipts")]
    [InlineData("suppliers", "stock-room", "suppliers")]
    [InlineData("adjustments", "stock-room", "adjustments")]
    [InlineData("movements", "stock-room", "movements")]
    public void ExistingRoutes_KeepEveryWorkspaceAndInventoryFunctionReachable(string tag, string destination, string? section)
    {
        var route = ServiceConsoleNavigation.Resolve(tag);
        Assert.Equal(destination, route.Destination.Tag);
        Assert.Equal(section, route.StockSection);
    }

    [Fact]
    public void PrimaryDestinations_AreExactlyTheSixConceptWorkspaces()
    {
        Assert.Equal(["counter", "service-board", "menu", "stock-room", "customers", "reports"],
            ServiceConsoleNavigation.PrimaryDestinations.Select(item => item.Tag));
        Assert.All(ServiceConsoleNavigation.PrimaryDestinations, item => Assert.Equal(item, ServiceConsoleNavigation.Resolve(item.Tag).Destination));
        Assert.Equal("counter", ServiceConsoleNavigation.DefaultTag);
        Assert.Throws<ArgumentException>(() => ServiceConsoleNavigation.Resolve("unknown"));
    }

    [Theory]
    [InlineData(640, ServiceConsoleLayoutMode.Compact)]
    [InlineData(641, ServiceConsoleLayoutMode.Medium)]
    [InlineData(1007, ServiceConsoleLayoutMode.Medium)]
    [InlineData(1008, ServiceConsoleLayoutMode.Expanded)]
    public void Layout_PreservesEstablishedBreakpointBoundaries(double width, ServiceConsoleLayoutMode expected) =>
        Assert.Equal(expected, ServiceConsoleNavigation.LayoutFor(width));

    [Fact]
    public void Connection_RetryBlocksNavigationUntilSuccessful()
    {
        var connection = new ServiceConsoleConnection();
        Assert.False(connection.IsConnected);
        connection.Failed("Database is unavailable. Retry connection.");
        Assert.False(connection.IsChecking);
        Assert.Contains("Retry", connection.Failure);
        connection.Begin();
        Assert.False(connection.IsConnected);
        Assert.Null(connection.Failure);
        connection.Connected();
        Assert.True(connection.IsConnected);
        Assert.Equal("LOCAL · CONNECTED", connection.Status);
    }
}
