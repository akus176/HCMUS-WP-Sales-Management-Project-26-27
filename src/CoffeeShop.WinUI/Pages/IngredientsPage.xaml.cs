using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class IngredientsPage : Page
{
    private InventoryCommandHandler handler = null!;
    private IInventoryRepository repository = null!;
    private List<IngredientRow> allRows = [];
    private bool isBusy;

    public IngredientsPage()
    {
        InitializeComponent();
        _ = LoadAsync();
    }

    private Task LoadAsync(string? successMessage = null) => RunAsync("load ingredients", async () =>
    {
        var ingredients = await repository.ListIngredientsAsync();
        allRows = ingredients.Select(i => new IngredientRow(i)).ToList();
        ApplyFilter();
        Status.Apply(successMessage is not null
            ? WorkflowFeedback.Success(successMessage)
            : allRows.Count == 0
                ? WorkflowFeedback.Empty("No ingredients are available.", "Create an ingredient to begin tracking stock.")
                : WorkflowFeedback.Ready("Ingredients loaded. Search, edit, or change availability."));
    });

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim();
        var filtered = string.IsNullOrEmpty(query)
            ? allRows
            : allRows.Where(row => row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        IngredientsList.ItemsSource = filtered;
        EmptyMessage.Visibility = !isBusy && filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs args) => ApplyFilter();

    private async void OnReload(object sender, RoutedEventArgs args) => await LoadAsync();

    private async void OnAddIngredient(object sender, RoutedEventArgs args)
    {
        var dialog = new IngredientDialog();
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        await RunAsync("create the ingredient", async () =>
        {
            var result = await handler.CreateIngredientAsync(new CreateIngredientCommand(
                dialog.IngredientName, dialog.BaseUnit, dialog.AvailableQuantity, dialog.ReorderThreshold));
            if (result.IsSuccess)
            {
                await LoadCoreAsync("Ingredient created.");
            }
            else
            {
                Status.Apply(WorkflowFeedback.FromError(result.Error!));
            }
        });
    }

    private async void OnEditIngredient(object sender, RoutedEventArgs args)
    {
        if ((sender as Button)?.Tag is not IngredientRow row)
        {
            return;
        }

        Ingredient? ingredient = null;
        await RunAsync("open the ingredient", async () =>
        {
            ingredient = await repository.FindIngredientAsync(row.Id);
            Status.Apply(ingredient is null
                ? WorkflowFeedback.FromError(Error.NotFound("inventory.ingredient_not_found", "The ingredient no longer exists."))
                : WorkflowFeedback.Ready("Ingredient loaded for editing."));
        });
        if (ingredient is null)
        {
            return;
        }

        var dialog = new IngredientDialog(ingredient);
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        await RunAsync("update the ingredient", async () =>
        {
            var threshold = await handler.SetReorderThresholdAsync(new SetReorderThresholdCommand(ingredient.Id, dialog.ReorderThreshold));
            if (threshold.IsSuccess)
            {
                await LoadCoreAsync("Ingredient updated.");
            }
            else
            {
                Status.Apply(WorkflowFeedback.FromError(threshold.Error!));
            }
        });
    }

    private async void OnToggleActive(object sender, RoutedEventArgs args)
    {
        if ((sender as Button)?.Tag is not IngredientRow row)
        {
            return;
        }

        await RunAsync("change ingredient availability", async () =>
        {
            Result result = row.IsActive
                ? await handler.DeactivateIngredientAsync(new DeactivateIngredientCommand(row.Id))
                : await handler.ActivateIngredientAsync(new ActivateIngredientCommand(row.Id));
            if (result.IsSuccess)
            {
                await LoadCoreAsync(row.IsActive ? "Ingredient deactivated." : "Ingredient activated.");
            }
            else
            {
                Status.Apply(WorkflowFeedback.FromError(result.Error!));
            }
        });
    }

    private static async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        dialog.XamlRoot = (App.Current as App)!.Window!.Content.XamlRoot;
        return await dialog.ShowAsync();
    }

    private async Task LoadCoreAsync(string successMessage)
    {
        var ingredients = await repository.ListIngredientsAsync();
        allRows = ingredients.Select(ingredient => new IngredientRow(ingredient)).ToList();
        ApplyFilter();
        Status.Apply(WorkflowFeedback.Success(successMessage));
    }

    private async Task RunAsync(string operation, Func<Task> action)
    {
        if (isBusy)
        {
            return;
        }

        SetBusy(true);
        Status.Apply(WorkflowFeedback.Loading($"Trying to {operation}..."));
        try
        {
            using var scope = App.Services.CreateScope();
            handler = scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>();
            repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
            await action();
        }
        catch (Exception)
        {
            Status.Apply(WorkflowFeedback.DatabaseFailure(operation,
                "Check the local database connection, then select Reload before retrying."));
        }
        finally
        {
            SetBusy(false);
            ApplyFilter();
        }
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        LoadingIndicator.IsActive = busy;
        LoadingIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ReloadButton.IsEnabled = !busy;
        AddButton.IsEnabled = !busy;
        SearchBox.IsEnabled = !busy;
    }
}
