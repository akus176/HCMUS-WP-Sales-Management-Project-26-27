using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class IngredientsPage : Page
{
    private readonly InventoryCommandHandler handler;
    private readonly IInventoryRepository repository;
    private List<IngredientRow> allRows = [];

    public IngredientsPage()
    {
        InitializeComponent();
        handler = App.Services.GetRequiredService<InventoryCommandHandler>();
        repository = App.Services.GetRequiredService<IInventoryRepository>();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var ingredients = await repository.ListIngredientsAsync();
            allRows = ingredients.Select(i => new IngredientRow(i)).ToList();
            ApplyFilter();
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, $"Could not load ingredients: {exception.Message}");
        }
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim();
        var filtered = string.IsNullOrEmpty(query)
            ? allRows
            : allRows.Where(row => row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        IngredientsList.ItemsSource = filtered;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs args) => ApplyFilter();

    private async void OnAddIngredient(object sender, RoutedEventArgs args)
    {
        var dialog = new IngredientDialog();
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        var result = await handler.CreateIngredientAsync(new CreateIngredientCommand(
            dialog.IngredientName, dialog.BaseUnit, dialog.AvailableQuantity, dialog.ReorderThreshold));
        if (result.IsSuccess)
        {
            ShowStatus(InfoBarSeverity.Success, "Ingredient created.");
            await LoadAsync();
        }
        else
        {
            ShowStatus(InfoBarSeverity.Error, result.Error!.Description);
        }
    }

    private async void OnEditIngredient(object sender, RoutedEventArgs args)
    {
        if ((sender as Button)?.Tag is not IngredientRow row)
        {
            return;
        }

        var ingredient = await repository.FindIngredientAsync(row.Id);
        if (ingredient is null)
        {
            ShowStatus(InfoBarSeverity.Error, "The ingredient no longer exists.");
            await LoadAsync();
            return;
        }

        var dialog = new IngredientDialog(ingredient);
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        var threshold = await handler.SetReorderThresholdAsync(new SetReorderThresholdCommand(ingredient.Id, dialog.ReorderThreshold));
        if (threshold.IsSuccess)
        {
            ShowStatus(InfoBarSeverity.Success, "Ingredient updated.");
            await LoadAsync();
        }
        else
        {
            ShowStatus(InfoBarSeverity.Error, threshold.Error!.Description);
        }
    }

    private async void OnToggleActive(object sender, RoutedEventArgs args)
    {
        if ((sender as Button)?.Tag is not IngredientRow row)
        {
            return;
        }

        Result result = row.IsActive
            ? await handler.DeactivateIngredientAsync(new DeactivateIngredientCommand(row.Id))
            : await handler.ActivateIngredientAsync(new ActivateIngredientCommand(row.Id));

        if (result.IsSuccess)
        {
            ShowStatus(InfoBarSeverity.Success, row.IsActive ? "Ingredient deactivated." : "Ingredient activated.");
            await LoadAsync();
        }
        else
        {
            ShowStatus(InfoBarSeverity.Error, result.Error!.Description);
        }
    }

    private static async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        dialog.XamlRoot = (App.Current as App)!.Window!.Content.XamlRoot;
        return await dialog.ShowAsync();
    }

    private void ShowStatus(InfoBarSeverity severity, string message)
    {
        Status.Severity = severity;
        Status.Message = message;
        Status.IsOpen = true;
    }
}
