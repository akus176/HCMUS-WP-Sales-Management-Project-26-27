using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

namespace CoffeeShop.WinUI.Pages;

/// <summary>
/// Read-model row for receipt lines in the detail panel.
/// </summary>
public sealed record ReceiptLineRow
{
    public ReceiptLineRow(PurchaseReceiptLine line, string ingredientName)
    {
        Id = line.Id;
        IngredientName = ingredientName;
        QuantityDisplay = $"{line.Quantity:0.###}";
        UnitCostDisplay = line.UnitCost.Amount.ToString("0.00");
        LineTotalDisplay = (line.Quantity * line.UnitCost.Amount).ToString("0.00");
    }

    public EntityId Id { get; }

    public string IngredientName { get; }

    public string QuantityDisplay { get; }

    public string UnitCostDisplay { get; }

    public string LineTotalDisplay { get; }
}
