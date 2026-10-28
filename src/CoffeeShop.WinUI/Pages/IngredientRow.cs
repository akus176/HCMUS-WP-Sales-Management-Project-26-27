using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

namespace CoffeeShop.WinUI.Pages;

/// <summary>
/// Read-model row for the ingredients list. Formatting lives here so the
/// XAML binding stays simple.
/// </summary>
public sealed record IngredientRow
{
    public IngredientRow(Ingredient ingredient, StockMovement? lastMovement = null, string? actorName = null)
    {
        Id = ingredient.Id;
        Name = ingredient.Name;
        BaseUnit = ingredient.BaseUnit;
        AvailableQuantity = ingredient.AvailableQuantity;
        ReorderThreshold = ingredient.ReorderThreshold;
        BalanceDisplay = $"{ingredient.AvailableQuantity:#,0.###} {ingredient.BaseUnit}";
        ThresholdDisplay = $"{ingredient.ReorderThreshold:#,0.###} {ingredient.BaseUnit}";
        StatusDisplay = ingredient.Status == StockStatus.Ok ? "OK" : "Low stock";
        ToggleActiveLabel = ingredient.IsActive ? "Deactivate" : "Activate";
        IsActive = ingredient.IsActive;
        BalanceNumber = ingredient.AvailableQuantity.ToString("#,0.###");
        ThresholdNumber = ingredient.ReorderThreshold.ToString("#,0.###");
        LastMovementDisplay = lastMovement is null
            ? "No movements"
            : $"{lastMovement.QuantityDelta:+#,0.###;-#,0.###;0} {BaseUnit} · {MovementLabel(lastMovement.Type)}";
        LastMovementAt = lastMovement?.OccurredAt.Value;
        LastMovementMeta = lastMovement is null
            ? ""
            : $"{FormatWhen(lastMovement.OccurredAt.Value.ToLocalTime())} · {actorName ?? "Staff"}";
        LastMovementTooltip = lastMovement is null
            ? "No stock movements recorded for this ingredient."
            : $"{lastMovement.OccurredAt.Value.ToLocalTime():g} · {lastMovement.Reason} · {actorName ?? "Staff"}";
    }

    public EntityId Id { get; }

    public string Name { get; }

    public string BaseUnit { get; }
    public string UnitSuffix => " " + BaseUnit;

    public decimal AvailableQuantity { get; }
    public decimal ReorderThreshold { get; }
    public DateTimeOffset? LastMovementAt { get; }

    public string BalanceDisplay { get; }

    public string ThresholdDisplay { get; }

    public string BalanceNumber { get; }
    public string ThresholdNumber { get; }
    public string LastMovementDisplay { get; }
    public string LastMovementMeta { get; }
    public string LastMovementTooltip { get; }
    public string ShortStatus => !IsActive ? "INACTIVE" : StatusDisplay == "OK" ? "✓ OK" : "LOW";
    public double LowStockMarkerOpacity => IsActive && StatusDisplay != "OK" ? 1 : 0;

    public bool IsLowStock => IsActive && StatusDisplay != "OK";

    public int StatusRank => !IsActive ? 2 : IsLowStock ? 0 : 1;

    public string RowAccessibilityName =>
        $"{Name}. {BalanceNumber} {BaseUnit} on hand. Reorder at {ThresholdNumber} {BaseUnit}. " +
        (IsLowStock ? "Low stock. " : IsActive ? "Stock OK. " : "Inactive. ") +
        LastMovementDisplay + (string.IsNullOrEmpty(LastMovementMeta) ? "" : $" at {LastMovementMeta}.");

    public string StatusDisplay { get; }

    public string ToggleActiveLabel { get; }

    public bool IsActive { get; }

    public string UnitDisplay => IsActive ? $"Unit: {BaseUnit}" : $"Inactive · {BaseUnit}";

    public string AdjustActionName => $"Adjust {Name}";

    private static string MovementLabel(StockMovementType type) => type switch
    {
        StockMovementType.OrderConsumption => "service",
        StockMovementType.Restoration => "restoration",
        StockMovementType.Receipt => "receipt",
        _ => "adjustment",
    };

    /// <summary>
    /// Movement timestamps stay terse for today, but gain day context as soon
    /// as "10:36" would be ambiguous.
    /// </summary>
    public static string FormatWhen(DateTimeOffset local, DateTimeOffset? now = null)
    {
        var today = (now ?? DateTimeOffset.Now).Date;
        if (local.Date == today) return local.ToString("HH:mm");
        if (local.Date == today.AddDays(-1)) return "Yesterday";
        return local.Year == today.Year ? local.ToString("d MMM") : local.ToString("d MMM yyyy");
    }
}
