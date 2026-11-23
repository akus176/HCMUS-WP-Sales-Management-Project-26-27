namespace CoffeeShop.WinUI.ViewModels;

public enum ServiceConsoleWorkspace { Counter, ServiceBoard, Menu, StockRoom, Customers, Reports, Accounts }
public enum ServiceConsoleLayoutMode { Compact, Medium, Expanded }

public sealed record ServiceConsoleDestination(string Tag, string Label, string Numeral, string Group, ServiceConsoleWorkspace Workspace);
public sealed record ServiceConsoleRoute(ServiceConsoleDestination Destination, string? StockSection = null);

public static class ServiceConsoleNavigation
{
    public const string DefaultTag = "counter";
    private static readonly IReadOnlyDictionary<string, string[]> DestinationPermissions =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["counter"] = ["orders.create"],
            ["service-board"] = ["orders.fulfill", "delivery.update"],
            ["menu"] = ["menu.manage"],
            ["stock-room"] = ["inventory.manage", "inventory.receive", "inventory.adjust"],
            ["customers"] = ["customers.manage"],
            ["reports"] = ["reports.revenue", "reports.bestsellers", "reports.channels", "reports.lowstock"],
            ["accounts"] = ["users.manage"],
        };
    public static IReadOnlyList<ServiceConsoleDestination> PrimaryDestinations { get; } = Array.AsReadOnly<ServiceConsoleDestination>(
    [
        new("counter", "Counter", "I", "Service", ServiceConsoleWorkspace.Counter),
        new("service-board", "Service board", "II", "Service", ServiceConsoleWorkspace.ServiceBoard),
        new("menu", "Menu", "III", "Manage", ServiceConsoleWorkspace.Menu),
        new("stock-room", "Stock room", "IV", "Manage", ServiceConsoleWorkspace.StockRoom),
        new("customers", "Customers", "V", "Manage", ServiceConsoleWorkspace.Customers),
        new("reports", "Reports", "VI", "Insights", ServiceConsoleWorkspace.Reports),
    ]);
    private static IReadOnlyList<ServiceConsoleDestination> AuthorizedDestinations { get; } =
        [.. PrimaryDestinations, new("accounts", "Accounts", "VII", "Administration", ServiceConsoleWorkspace.Accounts)];
    public static IReadOnlyDictionary<string, string> StockSections { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["ingredients"] = "Balances",
            ["lowstock"] = "Low stock",
            ["receipts"] = "Receipts",
            ["suppliers"] = "Suppliers",
            ["adjustments"] = "Adjustments",
            ["movements"] = "Movements",
        });

    public static ServiceConsoleRoute Resolve(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        var key = tag.ToLowerInvariant();
        var section = StockSections.ContainsKey(key) ? key : null;
        var primary = key switch
        {
            "pos" => "counter",
            "operations" => "service-board",
            "catalog" => "menu",
            _ when section is not null => "stock-room",
            _ => key,
        };
        var destination = AuthorizedDestinations.SingleOrDefault(item => item.Tag == primary)
            ?? throw new ArgumentException("Unknown service-console destination.", nameof(tag));
        return new(destination, section ?? (primary == "stock-room" ? "ingredients" : null));
    }

    public static IReadOnlyList<ServiceConsoleDestination> ForPermissions(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        var effectivePermissions = permissions.ToHashSet(StringComparer.Ordinal);
        return AuthorizedDestinations
            .Where(destination => DestinationPermissions[destination.Tag].Any(effectivePermissions.Contains))
            .ToArray();
    }

    public static ServiceConsoleRoute Resolve(string tag, IEnumerable<string> permissions)
    {
        var route = Resolve(tag);
        if (!ForPermissions(permissions).Any(destination => destination.Tag == route.Destination.Tag))
            throw new UnauthorizedAccessException("The signed-in staff member cannot open this workspace.");
        return route;
    }

    public static ServiceConsoleLayoutMode LayoutFor(double width)
    {
        if (!double.IsFinite(width) || width < 0) throw new ArgumentOutOfRangeException(nameof(width));
        return width < 641 ? ServiceConsoleLayoutMode.Compact
            : width < 1008 ? ServiceConsoleLayoutMode.Medium : ServiceConsoleLayoutMode.Expanded;
    }
}

public sealed class ServiceConsoleConnection
{
    public bool IsConnected { get; private set; }
    public bool IsChecking { get; private set; } = true;
    public string? Failure { get; private set; }
    public string Status => IsChecking ? "CHECKING CONNECTION" : IsConnected ? "LOCAL · CONNECTED" : "LOCAL · OFFLINE";
    public void Begin() { IsConnected = false; IsChecking = true; Failure = null; }
    public void Connected() { IsConnected = true; IsChecking = false; Failure = null; }
    public void Failed(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        IsConnected = false; IsChecking = false; Failure = message;
    }
}
