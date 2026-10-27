using CoffeeShop.WinUI.ViewModels;

namespace CoffeeShop.WinUI.Tests;

public sealed class ServiceConsoleShellTests
{
    [Fact]
    public void PrimaryNavigation_ExposesSixTaskOrientedDestinations()
    {
        Assert.Collection(
            ServiceConsoleNavigation.PrimaryDestinations,
            item => AssertDestination(item, "counter", "Counter", ServiceConsoleWorkspace.Counter),
            item => AssertDestination(item, "service-board", "Service board", ServiceConsoleWorkspace.ServiceBoard),
            item => AssertDestination(item, "menu", "Menu", ServiceConsoleWorkspace.Menu),
            item => AssertDestination(item, "stock-room", "Stock room", ServiceConsoleWorkspace.StockRoom),
            item => AssertDestination(item, "customers", "Customers", ServiceConsoleWorkspace.Customers),
            item => AssertDestination(item, "reports", "Reports", ServiceConsoleWorkspace.Reports));
        Assert.Equal("counter", ServiceConsoleNavigation.DefaultTag);
    }

    [Theory]
    [InlineData("pos", ServiceConsoleWorkspace.Counter)]
    [InlineData("operations", ServiceConsoleWorkspace.ServiceBoard)]
    [InlineData("catalog", ServiceConsoleWorkspace.Menu)]
    [InlineData("ingredients", ServiceConsoleWorkspace.StockRoom)]
    [InlineData("suppliers", ServiceConsoleWorkspace.StockRoom)]
    [InlineData("receipts", ServiceConsoleWorkspace.StockRoom)]
    [InlineData("lowstock", ServiceConsoleWorkspace.StockRoom)]
    [InlineData("adjustments", ServiceConsoleWorkspace.StockRoom)]
    [InlineData("movements", ServiceConsoleWorkspace.StockRoom)]
    [InlineData("customers", ServiceConsoleWorkspace.Customers)]
    [InlineData("reports", ServiceConsoleWorkspace.Reports)]
    public void LegacyNavigation_ResolvesToStableWorkspace(string tag, ServiceConsoleWorkspace workspace)
    {
        Assert.Equal(workspace, ServiceConsoleNavigation.Resolve(tag).Workspace);
    }

    [Theory]
    [InlineData(0, ServiceConsoleLayoutMode.Compact)]
    [InlineData(640, ServiceConsoleLayoutMode.Compact)]
    [InlineData(641, ServiceConsoleLayoutMode.Medium)]
    [InlineData(1007, ServiceConsoleLayoutMode.Medium)]
    [InlineData(1008, ServiceConsoleLayoutMode.Expanded)]
    public void ShellLayout_UsesEstablishedBreakpoints(double width, ServiceConsoleLayoutMode expected)
    {
        Assert.Equal(expected, ServiceConsoleLayout.ForWidth(width));
    }

    [Fact]
    public void NavigationItems_ExposeAccessibleNamesAndFunctionalGlyphs()
    {
        Assert.All(ServiceConsoleNavigation.PrimaryDestinations, destination =>
        {
            Assert.False(string.IsNullOrWhiteSpace(destination.AccessibleName));
            Assert.False(string.IsNullOrWhiteSpace(destination.Glyph));
        });
    }

    [Fact]
    public void PrimaryControls_UseMinimumFortyEightDipTouchTargets()
    {
        Assert.Equal(48, ServiceConsoleAccessibility.MinimumPrimaryTouchTarget);
    }

    [Fact]
    public void ConnectionPresentation_BlocksNavigationUntilConnected()
    {
        var checking = ServiceConsoleConnectionPresentation.Checking();
        var failed = ServiceConsoleConnectionPresentation.Failed("Database is unavailable.");
        var connected = ServiceConsoleConnectionPresentation.Connected("environment");

        Assert.False(checking.IsNavigationEnabled);
        Assert.False(checking.ShowBlockingFailure);
        Assert.False(checking.ShowRetry);

        Assert.False(failed.IsNavigationEnabled);
        Assert.True(failed.ShowBlockingFailure);
        Assert.True(failed.ShowRetry);
        Assert.Contains("unavailable", failed.DetailText);

        Assert.True(connected.IsNavigationEnabled);
        Assert.False(connected.ShowBlockingFailure);
        Assert.False(connected.ShowRetry);
        Assert.Equal("Local · connected", connected.StatusText);
        Assert.Contains("environment", connected.StatusDetail);
    }

    private static void AssertDestination(
        ServiceConsoleDestination destination,
        string tag,
        string label,
        ServiceConsoleWorkspace workspace)
    {
        Assert.Equal(tag, destination.Tag);
        Assert.Equal(label, destination.Label);
        Assert.Equal(workspace, destination.Workspace);
    }
}
