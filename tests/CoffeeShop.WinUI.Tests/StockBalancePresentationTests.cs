using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.WinUI.Pages;

namespace CoffeeShop.WinUI.Tests;

public sealed class StockBalancePresentationTests
{
    [Fact]
    public void InactiveIngredient_IsIdentifiedInsteadOfBeingPresentedAsAnActiveLowBalance()
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var row = new IngredientRow(new Ingredient(EntityId.New(), "Archived beans", "g", now, now,
            availableQuantity: 0, reorderThreshold: 100, isActive: false));
        Assert.Equal("INACTIVE", row.ShortStatus);
        Assert.Contains("Inactive", row.UnitDisplay);
        Assert.Equal(0, row.LowStockMarkerOpacity);
    }

    [Fact]
    public void LowBalance_UsesTheRealMovementQuantityUnitAndActor()
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var ingredient = new Ingredient(EntityId.New(), "Arabica", "g", now, now,
            availableQuantity: 500m, reorderThreshold: 500m);
        var movement = new StockMovement(EntityId.New(), ingredient.Id, StockMovementType.Adjustment,
            -120m, EntityId.New(), "Calibration waste", now);

        var row = new IngredientRow(ingredient, movement, "Mai Tran");

        Assert.Equal("500", row.BalanceNumber);
        Assert.Equal("500", row.ThresholdNumber);
        Assert.Equal("LOW", row.ShortStatus);
        Assert.Equal(1, row.LowStockMarkerOpacity);
        Assert.Equal("-120 g · adjustment", row.LastMovementDisplay);
        Assert.Contains("Mai Tran", row.LastMovementMeta);
        Assert.Equal(" g", row.UnitSuffix);
    }

    [Fact]
    public void BalanceWithoutHistory_DoesNotInventMovementDetails()
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var row = new IngredientRow(new Ingredient(EntityId.New(), "Milk", "ml", now, now,
            availableQuantity: 3000m, reorderThreshold: 1000m));

        Assert.Equal("✓ OK", row.ShortStatus);
        Assert.Equal(0, row.LowStockMarkerOpacity);
        Assert.Equal("No movements", row.LastMovementDisplay);
        Assert.Empty(row.LastMovementMeta);
    }

    [Fact]
    public void LargeQuantities_UseGroupedFiguresAndExposeSortMetadata()
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var ingredient = new Ingredient(EntityId.New(), "Beans", "g", now, now,
            availableQuantity: 2000m, reorderThreshold: 500m);
        var movement = new StockMovement(EntityId.New(), ingredient.Id, StockMovementType.Receipt,
            2500m, EntityId.New(), "Delivery", now, purchaseReceiptId: EntityId.New());

        var row = new IngredientRow(ingredient, movement, "Bao Le");

        Assert.Equal(2000m.ToString("#,0.###"), row.BalanceNumber);
        Assert.Equal(2000m, row.AvailableQuantity);
        Assert.Equal(500m, row.ReorderThreshold);
        Assert.Equal($"+{2500m.ToString("#,0.###")} g · receipt", row.LastMovementDisplay);
        Assert.Equal(1, row.StatusRank);
        Assert.Contains($"{2000m.ToString("#,0.###")} g on hand", row.RowAccessibilityName);
        Assert.Contains("Stock OK", row.RowAccessibilityName);
    }

    [Fact]
    public void StatusRank_PutsShortagesFirstAndInactiveItemsLast()
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var low = new IngredientRow(new Ingredient(EntityId.New(), "Low", "g", now, now,
            availableQuantity: 10m, reorderThreshold: 100m));
        var ok = new IngredientRow(new Ingredient(EntityId.New(), "Ok", "g", now, now,
            availableQuantity: 200m, reorderThreshold: 100m));
        var inactive = new IngredientRow(new Ingredient(EntityId.New(), "Inactive", "g", now, now,
            availableQuantity: 100m, reorderThreshold: 100m, isActive: false));

        Assert.Equal(0, low.StatusRank);
        Assert.Equal(1, ok.StatusRank);
        Assert.Equal(2, inactive.StatusRank);
        Assert.Contains("Low stock", low.RowAccessibilityName);
        Assert.Contains("Inactive", inactive.RowAccessibilityName);
    }

    [Fact]
    public void MovementTimestamps_KeepDayContextOnceTheyLeaveToday()
    {
        var now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("10:36", IngredientRow.FormatWhen(new DateTimeOffset(2026, 10, 28, 10, 36, 0, TimeSpan.Zero), now));
        Assert.Equal("Yesterday", IngredientRow.FormatWhen(new DateTimeOffset(2026, 10, 27, 10, 36, 0, TimeSpan.Zero), now));
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 10, 36, 0, TimeSpan.Zero).ToString("d MMM"),
            IngredientRow.FormatWhen(new DateTimeOffset(2026, 10, 24, 10, 36, 0, TimeSpan.Zero), now));
    }
}
