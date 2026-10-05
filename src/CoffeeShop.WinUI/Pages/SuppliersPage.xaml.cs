using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class SuppliersPage : Page
{
    private readonly InventoryCommandHandler handler;
    private readonly IInventoryRepository repository;
    private List<SupplierRow> allRows = [];

    public SuppliersPage()
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
            allRows = suppliers.Select(s => new SupplierRow(s)).ToList();
            SuppliersList.ItemsSource = allRows;
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, $"Could not load suppliers: {exception.Message}");
        }
    }

    private async void OnAddSupplier(object sender, RoutedEventArgs args)
    {
        var dialog = new SupplierDialog();
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        var result = await handler.CreateSupplierAsync(new CreateSupplierCommand(
            dialog.SupplierName, dialog.Phone, dialog.Email, dialog.Address));
        if (result.IsSuccess)
        {
            ShowStatus(InfoBarSeverity.Success, "Supplier created.");
            await LoadAsync();
        }
        else
        {
            ShowStatus(InfoBarSeverity.Error, result.Error!.Description);
        }
    }

    private async void OnEditSupplier(object sender, RoutedEventArgs args)
    {
        if ((sender as Button)?.Tag is not SupplierRow row)
        {
            return;
        }

        var supplier = await repository.FindSupplierAsync(row.Id);
        if (supplier is null)
        {
            ShowStatus(InfoBarSeverity.Error, "The supplier no longer exists.");
            await LoadAsync();
            return;
        }

        var dialog = new SupplierDialog(supplier);
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        var result = await handler.UpdateSupplierAsync(new UpdateSupplierCommand(
            supplier.Id, dialog.SupplierName, dialog.Phone, dialog.Email, dialog.Address));
        if (result.IsSuccess)
        {
            ShowStatus(InfoBarSeverity.Success, "Supplier updated.");
            await LoadAsync();
        }
        else
        {
            ShowStatus(InfoBarSeverity.Error, result.Error!.Description);
        }
    }

    private async void OnToggleActive(object sender, RoutedEventArgs args)
    {
        if ((sender as Button)?.Tag is not SupplierRow row)
        {
            return;
        }

        Result result = row.IsActive
            ? await handler.DeactivateSupplierAsync(new DeactivateSupplierCommand(row.Id))
            : await handler.ActivateSupplierAsync(new ActivateSupplierCommand(row.Id));

        if (result.IsSuccess)
        {
            ShowStatus(InfoBarSeverity.Success, row.IsActive ? "Supplier deactivated." : "Supplier activated.");
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
