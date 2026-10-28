using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

namespace CoffeeShop.WinUI.ViewModels;

public sealed record StockRoomTab(string Tag, string Label);
public sealed record StockRoomEntry(string Tab, bool LowStockOnly = false, bool Adjust = false);

public static class StockRoomInteraction
{
    public static IReadOnlyList<StockRoomTab> Tabs { get; } = Array.AsReadOnly<StockRoomTab>(
    [new("ingredients", "Inventory"), new("receipts", "Receipts"), new("suppliers", "Suppliers"), new("movements", "History")]);

    public static StockRoomEntry Resolve(string tag) => tag.ToLowerInvariant() switch
    {
        "ingredients" or "balances" => new("ingredients"),
        "lowstock" => new("ingredients", LowStockOnly: true),
        "adjustments" => new("ingredients", Adjust: true),
        "receipts" => new("receipts"),
        "suppliers" => new("suppliers"),
        "history" or "movements" => new("movements"),
        _ => throw new ArgumentException("Unknown Stock room function.", nameof(tag)),
    };
}

public sealed record StockAdjustmentDraft(string Quantity = "", string Reason = "");

public sealed class StockAdjustmentDrafts
{
    private readonly Dictionary<EntityId, StockAdjustmentDraft> drafts = [];
    public StockAdjustmentDraft For(EntityId id) => drafts.GetValueOrDefault(id) ?? new();
    public void Remember(EntityId id, string quantity, string reason)
    {
        if (string.IsNullOrWhiteSpace(quantity) && string.IsNullOrWhiteSpace(reason)) drafts.Remove(id);
        else drafts[id] = new(quantity, reason);
    }
    public void Clear(EntityId id) => drafts.Remove(id);
}

public sealed record StockAdjustmentPreview(decimal? Balance, bool CanSubmit, string Hint)
{
    public static StockAdjustmentPreview Create(Ingredient? ingredient, string quantity, bool hasActor)
    {
        if (ingredient is null) return new(null, false, "Select an ingredient first.");
        if (!ingredient.IsActive) return new(null, false, "Inactive ingredients cannot be adjusted.");
        if (!CatalogNumberInput.TryParse(quantity, out var delta)) return new(null, false, "Enter a quantity change, such as -5 or +10.");
        if (delta == 0) return new(ingredient.AvailableQuantity, false, "Enter a non-zero quantity change.");
        if (decimal.Round(delta, 3) != delta || delta < -MeasuredQuantity.MaximumValue || delta > MeasuredQuantity.MaximumValue)
            return new(null, false, "Use up to three decimal places within the stock quantity limit.");
        var balance = ingredient.AvailableQuantity + delta;
        if (balance < 0 || balance > MeasuredQuantity.MaximumValue) return new(balance, false, "The resulting balance must stay within the allowed stock range.");
        if (!hasActor) return new(balance, false, "Choose the staff operator for this movement.");
        return new(balance, true, "Ready to record. This movement will remain in the audit history.");
    }
}
