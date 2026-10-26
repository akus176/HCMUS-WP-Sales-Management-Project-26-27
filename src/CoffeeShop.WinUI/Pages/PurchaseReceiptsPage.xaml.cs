using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class PurchaseReceiptsPage : Page
{
    private InventoryCommandHandler handler = null!;
    private IInventoryRepository repository = null!;
    private readonly Dictionary<EntityId, string> supplierNames = [];
    private readonly Dictionary<EntityId, string> activeSupplierNames = [];
    private readonly Dictionary<EntityId, string> ingredientNames = [];
    private readonly Dictionary<EntityId, string> activeIngredientNames = [];
    private PurchaseReceipt? selectedReceipt;
    private bool isBusy;

    public PurchaseReceiptsPage()
    {
        InitializeComponent();
        _ = LoadAsync();
    }

    private Task LoadAsync() => RunAsync("load purchase receipts", async () =>
    {
        var count = await LoadCoreAsync();
        Status.Apply(count == 0
            ? WorkflowFeedback.Empty(
                "No purchase receipts are available.",
                activeSupplierNames.Count == 0 ? "Create or activate a supplier first, then create a draft receipt." : "Create a draft receipt to record incoming stock.")
            : WorkflowFeedback.Ready("Purchase receipts loaded. Select a draft to manage its lines."));
    });

    private async void OnReload(object sender, RoutedEventArgs args) => await LoadAsync();

    private async void OnNewReceipt(object sender, RoutedEventArgs args)
    {
        var dialog = new NewReceiptDialog(activeSupplierNames.Values.OrderBy(n => n).ToList());
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        var supplierId = activeSupplierNames.FirstOrDefault(kvp => kvp.Value == dialog.SupplierName).Key;
        if (supplierId is null)
        {
            Status.Apply(WorkflowFeedback.Validation("supplier", "Select an active supplier."));
            return;
        }

        await RunAsync("create the receipt", async () =>
        {
            var result = await handler.CreateDraftReceiptAsync(new CreateDraftReceiptCommand(
                supplierId, dialog.ReceiptNumber));
            if (result.IsSuccess)
            {
                await LoadCoreAsync();
                Status.Apply(WorkflowFeedback.Success("Draft receipt created."));
            }
            else
            {
                Status.Apply(WorkflowFeedback.FromError(result.Error!));
            }
        });
    }

    private async void OnReceiptSelected(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is not ReceiptRow row)
        {
            return;
        }

        PurchaseReceipt? receipt = null;
        await RunAsync("open the receipt", async () =>
        {
            receipt = await repository.FindReceiptWithLinesAsync(row.Id);
            Status.Apply(receipt is null
                ? WorkflowFeedback.FromError(Error.NotFound("inventory.receipt_not_found", "The receipt no longer exists."))
                : WorkflowFeedback.Ready("Receipt loaded. Only draft receipts can be changed or posted."));
        });
        if (receipt is null)
        {
            return;
        }

        selectedReceipt = receipt;
        DetailTitle.Text = $"{receipt.ReceiptNumber} — {receipt.Status}";
        LinesList.ItemsSource = receipt.Lines
            .Select(l => new ReceiptLineRow(l, ingredientNames.GetValueOrDefault(l.IngredientId, "(unknown)")))
            .ToList();
        DetailPanel.Visibility = Visibility.Visible;
    }

    private async void OnAddLine(object sender, RoutedEventArgs args)
    {
        if (selectedReceipt is null)
        {
            return;
        }

        var dialog = new ReceiptLineDialog(activeIngredientNames.Values.OrderBy(n => n).ToList());
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        var ingredientId = activeIngredientNames.FirstOrDefault(kvp => kvp.Value == dialog.IngredientName).Key;
        if (ingredientId is null)
        {
            Status.Apply(WorkflowFeedback.Validation("ingredient", "Select an active ingredient."));
            return;
        }

        await RunAsync("add the receipt line", async () =>
        {
            var result = await handler.AddReceiptLineAsync(new AddReceiptLineCommand(
                selectedReceipt.Id, ingredientId, dialog.Quantity, dialog.UnitCost));
            if (result.IsSuccess)
            {
                await RefreshSelectedReceiptAsync();
                Status.Apply(WorkflowFeedback.Success("Line added."));
            }
            else
            {
                Status.Apply(WorkflowFeedback.FromError(result.Error!));
            }
        });
    }

    private async void OnRemoveLine(object sender, RoutedEventArgs args)
    {
        if (selectedReceipt is null || (sender as Button)?.Tag is not ReceiptLineRow row)
        {
            return;
        }

        await RunAsync("remove the receipt line", async () =>
        {
            var result = await handler.RemoveReceiptLineAsync(new RemoveReceiptLineCommand(selectedReceipt.Id, row.Id));
            if (result.IsSuccess)
            {
                await RefreshSelectedReceiptAsync();
                Status.Apply(WorkflowFeedback.Success("Line removed."));
            }
            else
            {
                Status.Apply(WorkflowFeedback.FromError(result.Error!));
            }
        });
    }

    private async void OnPostReceipt(object sender, RoutedEventArgs args)
    {
        if (selectedReceipt is null)
        {
            return;
        }

        await RunAsync("post the receipt", async () =>
        {
            var actor = App.Services.GetRequiredService<PosOperatorSession>().Id;
            using var operatorScope = App.Services.CreateScope();
            var options = await operatorScope.ServiceProvider.GetRequiredService<PosOrderHandler>().GetOptionsAsync();
            if (actor is null || options.Staff.All(staff => staff.Id != actor))
            {
                Status.Apply(WorkflowFeedback.Validation("operator", "Select an active operator in Point of Sale or Stock Adjustments before posting a receipt."));
                return;
            }
            var result = await handler.PostReceiptAsync(new PostReceiptCommand(selectedReceipt.Id, actor));
            if (result.IsSuccess)
            {
                await LoadCoreAsync();
                Status.Apply(WorkflowFeedback.Success("Receipt posted; ingredient balances updated."));
            }
            else
            {
                Status.Apply(WorkflowFeedback.FromError(result.Error!));
                await RefreshSelectedReceiptAsync();
            }
        });
    }

    private async void OnCancelReceipt(object sender, RoutedEventArgs args)
    {
        if (selectedReceipt is null)
        {
            return;
        }

        await RunAsync("cancel the receipt", async () =>
        {
            var result = await handler.CancelReceiptAsync(new CancelReceiptCommand(selectedReceipt.Id));
            if (result.IsSuccess)
            {
                await LoadCoreAsync();
                Status.Apply(WorkflowFeedback.Success("Receipt cancelled."));
            }
            else
            {
                Status.Apply(WorkflowFeedback.FromError(result.Error!));
            }
        });
    }

    private async Task RefreshSelectedReceiptAsync()
    {
        if (selectedReceipt is null)
        {
            return;
        }

        var receipt = await repository.FindReceiptWithLinesAsync(selectedReceipt.Id);
        if (receipt is null)
        {
            return;
        }

        selectedReceipt = receipt;
        DetailTitle.Text = $"{receipt.ReceiptNumber} — {receipt.Status}";
        LinesList.ItemsSource = receipt.Lines
            .Select(l => new ReceiptLineRow(l, ingredientNames.GetValueOrDefault(l.IngredientId, "(unknown)")))
            .ToList();
    }

    private static async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        dialog.XamlRoot = (App.Current as App)!.Window!.Content.XamlRoot;
        return await dialog.ShowAsync();
    }

    private async Task<int> LoadCoreAsync()
    {
        var suppliers = await repository.ListSuppliersAsync();
        supplierNames.Clear();
        activeSupplierNames.Clear();
        foreach (var supplier in suppliers)
        {
            supplierNames[supplier.Id] = supplier.Name;
            if (supplier.IsActive)
            {
                activeSupplierNames[supplier.Id] = supplier.Name;
            }
        }

        var ingredients = await repository.ListIngredientsAsync();
        ingredientNames.Clear();
        activeIngredientNames.Clear();
        foreach (var ingredient in ingredients)
        {
            ingredientNames[ingredient.Id] = ingredient.Name;
            if (ingredient.IsActive)
            {
                activeIngredientNames[ingredient.Id] = ingredient.Name;
            }
        }

        var receipts = await repository.ListReceiptsAsync();
        var rows = receipts
            .Select(receipt => new ReceiptRow(receipt, supplierNames.GetValueOrDefault(receipt.SupplierId, "(missing)")))
            .ToList();
        ReceiptsList.ItemsSource = rows;
        EmptyMessage.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Collapsed;
        selectedReceipt = null;
        return rows.Count;
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
                "Check the local database connection, then select Reload and verify the saved state before retrying."));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        LoadingIndicator.IsActive = busy;
        LoadingIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ReloadButton.IsEnabled = !busy;
        NewReceiptButton.IsEnabled = !busy;
        ReceiptsList.IsEnabled = !busy;
    }
}
