namespace CoffeeShop.WinUI.ViewModels;

public enum ServiceConsoleWorkspace
{
    Counter,
    ServiceBoard,
    Menu,
    StockRoom,
    Customers,
    Reports,
}

public sealed record ServiceConsoleDestination(
    string Tag,
    string Label,
    string AccessibleName,
    string Glyph,
    ServiceConsoleWorkspace Workspace);

public static class ServiceConsoleNavigation
{
    public const string DefaultTag = "counter";

    public static IReadOnlyList<ServiceConsoleDestination> PrimaryDestinations { get; } =
    [
        new("counter", "Counter", "Open Counter workspace", "\uE719", ServiceConsoleWorkspace.Counter),
        new("service-board", "Service board", "Open Service board workspace", "\uE768", ServiceConsoleWorkspace.ServiceBoard),
        new("menu", "Menu", "Open Menu workspace", "\uE8F1", ServiceConsoleWorkspace.Menu),
        new("stock-room", "Stock room", "Open Stock room workspace", "\uE7B8", ServiceConsoleWorkspace.StockRoom),
        new("customers", "Customers", "Open Customers workspace", "\uE716", ServiceConsoleWorkspace.Customers),
        new("reports", "Reports", "Open Reports workspace", "\uE9D2", ServiceConsoleWorkspace.Reports),
    ];

    private static IReadOnlyDictionary<string, string> LegacyAliases { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["pos"] = "counter",
            ["operations"] = "service-board",
            ["catalog"] = "menu",
            ["ingredients"] = "stock-room",
            ["suppliers"] = "stock-room",
            ["receipts"] = "stock-room",
            ["lowstock"] = "stock-room",
            ["adjustments"] = "stock-room",
            ["movements"] = "stock-room",
        };

    public static ServiceConsoleDestination Resolve(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        var normalized = LegacyAliases.GetValueOrDefault(tag, tag);
        return PrimaryDestinations.Single(destination =>
            string.Equals(destination.Tag, normalized, StringComparison.OrdinalIgnoreCase));
    }
}

public enum ServiceConsoleLayoutMode
{
    Compact,
    Medium,
    Expanded,
}

public static class ServiceConsoleLayout
{
    public const double MediumMinimumWidth = 641;
    public const double ExpandedMinimumWidth = 1008;

    public static ServiceConsoleLayoutMode ForWidth(double width) => width switch
    {
        < 0 => throw new ArgumentOutOfRangeException(nameof(width)),
        < MediumMinimumWidth => ServiceConsoleLayoutMode.Compact,
        < ExpandedMinimumWidth => ServiceConsoleLayoutMode.Medium,
        _ => ServiceConsoleLayoutMode.Expanded,
    };
}

public static class ServiceConsoleAccessibility
{
    public const double MinimumPrimaryTouchTarget = 48;
}

public sealed record ServiceConsoleConnectionPresentation(
    bool IsNavigationEnabled,
    bool ShowBlockingFailure,
    bool ShowRetry,
    string StatusText,
    string StatusDetail,
    string DetailText,
    string StatusBrushResourceKey)
{
    public static ServiceConsoleConnectionPresentation Checking() => new(
        false,
        false,
        false,
        "Checking connection",
        string.Empty,
        "Checking database connection...",
        "SketchTertiaryForegroundBrush");

    public static ServiceConsoleConnectionPresentation Connected(string source) => new(
        true,
        false,
        false,
        "Local · connected",
        $"direct PostgreSQL · {source}",
        $"Connected to PostgreSQL ({source}). The WinUI client is running in direct database mode.",
        "SketchSuccessBrush");

    public static ServiceConsoleConnectionPresentation Failed(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new(
            false,
            true,
            true,
            "Local · offline",
            "connection required",
            message,
            "SketchAccentBrush");
    }
}
