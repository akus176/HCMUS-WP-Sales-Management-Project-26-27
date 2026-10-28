using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

namespace CoffeeShop.WinUI.Pages;

/// <summary>
/// Read-model row for receipt lines in the detail panel.
/// </summary>
public sealed record ReceiptLineRow
{
    public ReceiptLineRow(PurchaseReceiptLine line, string ingredientName, string unit = "")
    {
        Id = line.Id;
        IngredientName = ingredientName;
        QuantityDisplay = $"{line.Quantity:#,0.###} {unit}".TrimEnd();
        UnitCostDisplay = line.UnitCost.Amount.ToString("0.00");
        LineTotal = line.Quantity * line.UnitCost.Amount;
        LineTotalDisplay = LineTotal.ToString("0.00");
        DetailDisplay = $"{QuantityDisplay} × {UnitCostDisplay}";
    }

    public EntityId Id { get; }

    public string IngredientName { get; }

    public string QuantityDisplay { get; }

    public string UnitCostDisplay { get; }

    public decimal LineTotal { get; }

    public string LineTotalDisplay { get; }

    public string DetailDisplay { get; }
}
