using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

namespace CoffeeShop.WinUI.Pages;

/// <summary>
/// Read-model row for the ingredients list. Formatting lives here so the
/// XAML binding stays simple.
/// </summary>
public sealed record IngredientRow
{
    public IngredientRow(Ingredient ingredient)
    {
        Id = ingredient.Id;
        Name = ingredient.Name;
        BaseUnit = ingredient.BaseUnit;
        BalanceDisplay = $"{ingredient.AvailableQuantity:0.###} {ingredient.BaseUnit}";
        ThresholdDisplay = $"{ingredient.ReorderThreshold:0.###} {ingredient.BaseUnit}";
        StatusDisplay = ingredient.Status == StockStatus.Ok ? "OK" : "Low stock";
        ToggleActiveLabel = ingredient.IsActive ? "Deactivate" : "Activate";
        IsActive = ingredient.IsActive;
    }

    public EntityId Id { get; }

    public string Name { get; }

    public string BaseUnit { get; }

    public string BalanceDisplay { get; }

    public string ThresholdDisplay { get; }

    public string StatusDisplay { get; }

    public string ToggleActiveLabel { get; }

    public bool IsActive { get; }

    public string UnitDisplay => $"Unit: {BaseUnit}";

    public string AdjustActionName => $"Adjust {Name}";
}
