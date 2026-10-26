using CoffeeShop.Application.Inventory;
using CoffeeShop.WinUI.ViewModels;
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
    private bool isBusy;

    public LowStockPage()
    {
        InitializeComponent();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (isBusy)
        {
            return;
        }

        SetBusy(true);
        Status.Apply(WorkflowFeedback.Loading("Checking current ingredient balances..."));
        try
        {
            using var scope = App.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
            var ingredients = await repository.ListIngredientsAsync();
            var lowStock = LowStockQuery.Find(ingredients);
            LowStockList.ItemsSource = lowStock.Select(i => new LowStockRow(i)).ToList();
            EmptyMessage.Visibility = lowStock.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Status.Apply(lowStock.Count == 0
                ? WorkflowFeedback.Empty(
                    "No active ingredients are at or below their reorder threshold.",
                    "No action is needed; refresh after stock balances change.")
                : WorkflowFeedback.Ready($"{lowStock.Count} ingredient(s) need replenishment."));
        }
        catch (Exception)
        {
            Status.Apply(WorkflowFeedback.DatabaseFailure(
                "load the low-stock report",
                "Check the local database connection, then select Refresh."));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnRefresh(object sender, RoutedEventArgs args) => await LoadAsync();

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        LoadingIndicator.IsActive = busy;
        LoadingIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !busy;
    }
}
