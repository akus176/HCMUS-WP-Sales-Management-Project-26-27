using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class PurchaseReceiptsPage : Page
{
    private readonly InventoryCommandHandler handler;
    private readonly IInventoryRepository repository;
    private readonly Dictionary<EntityId, string> supplierNames = [];
    private readonly Dictionary<EntityId, string> ingredientNames = [];
    private PurchaseReceipt? selectedReceipt;

    public PurchaseReceiptsPage()
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
            var suppliers = await repository.ListSuppliersAsync();
            supplierNames.Clear();
            foreach (var supplier in suppliers)
            {
                supplierNames[supplier.Id] = supplier.Name;
            }

            var ingredients = await repository.ListIngredientsAsync();
            ingredientNames.Clear();
            foreach (var ingredient in ingredients)
            {
                ingredientNames[ingredient.Id] = ingredient.Name;
            }

            var receipts = await repository.ListReceiptsAsync();
            ReceiptsList.ItemsSource = receipts
                .Select(r => new ReceiptRow(r, supplierNames.GetValueOrDefault(r.SupplierId, "(unknown)")))
                .ToList();
            DetailPanel.Visibility = Visibility.Collapsed;
            selectedReceipt = null;
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, $"Could not load receipts: {exception.Message}");
        }
    }

    private async void OnNewReceipt(object sender, RoutedEventArgs args)
    {
        var dialog = new NewReceiptDialog(supplierNames.Values.OrderBy(n => n).ToList());
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        var supplierId = supplierNames.FirstOrDefault(kvp => kvp.Value == dialog.SupplierName).Key;
        if (supplierId is null)
        {
            ShowStatus(InfoBarSeverity.Error, "Select a supplier.");
            return;
        }

        var result = await handler.CreateDraftReceiptAsync(new CreateDraftReceiptCommand(
            supplierId, dialog.ReceiptNumber));
        if (result.IsSuccess)
        {
            ShowStatus(InfoBarSeverity.Success, "Draft receipt created.");
            await LoadAsync();
        }
        else
        {
            ShowStatus(InfoBarSeverity.Error, result.Error!.Description);
        }
    }

    private async void OnReceiptSelected(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is not ReceiptRow row)
        {
            return;
        }

        var receipt = await repository.FindReceiptWithLinesAsync(row.Id);
        if (receipt is null)
        {
            ShowStatus(InfoBarSeverity.Error, "The receipt no longer exists.");
            await LoadAsync();
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

        var dialog = new ReceiptLineDialog(ingredientNames.Values.OrderBy(n => n).ToList());
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        var ingredientId = ingredientNames.FirstOrDefault(kvp => kvp.Value == dialog.IngredientName).Key;
        if (ingredientId is null)
        {
            ShowStatus(InfoBarSeverity.Error, "Select an ingredient.");
            return;
        }

        var result = await handler.AddReceiptLineAsync(new AddReceiptLineCommand(
            selectedReceipt.Id, ingredientId, dialog.Quantity, dialog.UnitCost));
        if (result.IsSuccess)
        {
            await RefreshSelectedReceiptAsync();
            ShowStatus(InfoBarSeverity.Success, "Line added.");
        }
        else
        {
            ShowStatus(InfoBarSeverity.Error, result.Error!.Description);
        }
    }

    private async void OnRemoveLine(object sender, RoutedEventArgs args)
    {
        if (selectedReceipt is null || (sender as Button)?.Tag is not ReceiptLineRow row)
        {
            return;
        }

        var result = await handler.RemoveReceiptLineAsync(new RemoveReceiptLineCommand(selectedReceipt.Id, row.Id));
        if (result.IsSuccess)
        {
            await RefreshSelectedReceiptAsync();
            ShowStatus(InfoBarSeverity.Success, "Line removed.");
        }
        else
        {
            ShowStatus(InfoBarSeverity.Error, result.Error!.Description);
        }
    }

    private async void OnPostReceipt(object sender, RoutedEventArgs args)
    {
        if (selectedReceipt is null)
        {
            return;
        }

        var result = await handler.PostReceiptAsync(new PostReceiptCommand(selectedReceipt.Id));
        if (result.IsSuccess)
        {
            ShowStatus(InfoBarSeverity.Success, "Receipt posted; ingredient balances updated.");
            await LoadAsync();
        }
        else
        {
            ShowStatus(InfoBarSeverity.Error, result.Error!.Description);
            await RefreshSelectedReceiptAsync();
        }
    }

    private async void OnCancelReceipt(object sender, RoutedEventArgs args)
    {
        if (selectedReceipt is null)
        {
            return;
        }

        var result = await handler.CancelReceiptAsync(new CancelReceiptCommand(selectedReceipt.Id));
        if (result.IsSuccess)
        {
            ShowStatus(InfoBarSeverity.Success, "Receipt cancelled.");
            await LoadAsync();
        }
        else
        {
            ShowStatus(InfoBarSeverity.Error, result.Error!.Description);
        }
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

    private void ShowStatus(InfoBarSeverity severity, string message)
    {
        Status.Severity = severity;
        Status.Message = message;
        Status.IsOpen = true;
    }
}
