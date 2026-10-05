using CoffeeShop.Application.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

/// <summary>
/// Read-model row for the low-stock list.
/// </summary>
public sealed record LowStockRow
{
    public LowStockRow(LowStockIngredient ingredient)
    {
        Name = ingredient.Name;
        BaseUnit = ingredient.BaseUnit;
        BalanceDisplay = $"{ingredient.AvailableQuantity:0.###} {ingredient.BaseUnit}";
        ThresholdDisplay = $"{ingredient.ReorderThreshold:0.###} {ingredient.BaseUnit}";
    }

    public string Name { get; }

    public string BaseUnit { get; }

    public string BalanceDisplay { get; }

    public string ThresholdDisplay { get; }
}

public sealed partial class LowStockPage : Page
{
    private readonly IInventoryRepository repository;

    public LowStockPage()
    {
        InitializeComponent();
        repository = App.Services.GetRequiredService<IInventoryRepository>();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var ingredients = await repository.ListIngredientsAsync();
            var lowStock = LowStockQuery.Find(ingredients);
            LowStockList.ItemsSource = lowStock.Select(i => new LowStockRow(i)).ToList();
            ShowStatus(InfoBarSeverity.Informational, $"{lowStock.Count} ingredient(s) at or below the reorder threshold.");
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, $"Could not load low stock: {exception.Message}");
        }
    }

    private async void OnRefresh(object sender, RoutedEventArgs args) => await LoadAsync();

    private void ShowStatus(InfoBarSeverity severity, string message)
    {
        Status.Severity = severity;
        Status.Message = message;
        Status.IsOpen = true;
    }
}
