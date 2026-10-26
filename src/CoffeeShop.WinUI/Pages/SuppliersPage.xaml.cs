using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class SuppliersPage : Page
{
    private InventoryCommandHandler handler = null!;
    private IInventoryRepository repository = null!;
    private List<SupplierRow> allRows = [];
    private bool isBusy;

    public SuppliersPage()
    {
        InitializeComponent();
        _ = LoadAsync();
    }

    private Task LoadAsync() => RunAsync("load suppliers", async () =>
    {
        await LoadCoreAsync();
        Status.Apply(allRows.Count == 0
            ? WorkflowFeedback.Empty("No suppliers are available.", "Create a supplier before starting a purchase receipt.")
            : WorkflowFeedback.Ready("Suppliers loaded. Select one to edit or change availability."));
    });

    private async void OnReload(object sender, RoutedEventArgs args) => await LoadAsync();

    private async void OnAddSupplier(object sender, RoutedEventArgs args)
    {
        var dialog = new SupplierDialog();
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        await RunAsync("create the supplier", async () =>
        {
            var result = await handler.CreateSupplierAsync(new CreateSupplierCommand(
                dialog.SupplierName, dialog.Phone, dialog.Email, dialog.Address));
            if (result.IsSuccess)
            {
                await LoadCoreAsync();
                Status.Apply(WorkflowFeedback.Success("Supplier created."));
            }
            else
            {
                Status.Apply(WorkflowFeedback.FromError(result.Error!));
            }
        });
    }

    private async void OnEditSupplier(object sender, RoutedEventArgs args)
    {
        if ((sender as Button)?.Tag is not SupplierRow row)
        {
            return;
        }

        Supplier? supplier = null;
        await RunAsync("open the supplier", async () =>
        {
            supplier = await repository.FindSupplierAsync(row.Id);
            Status.Apply(supplier is null
                ? WorkflowFeedback.FromError(Error.NotFound("inventory.supplier_not_found", "The supplier no longer exists."))
                : WorkflowFeedback.Ready("Supplier loaded for editing."));
        });
        if (supplier is null)
        {
            return;
        }

        var dialog = new SupplierDialog(supplier);
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        await RunAsync("update the supplier", async () =>
        {
            var result = await handler.UpdateSupplierAsync(new UpdateSupplierCommand(
                supplier.Id, dialog.SupplierName, dialog.Phone, dialog.Email, dialog.Address));
            if (result.IsSuccess)
            {
                await LoadCoreAsync();
                Status.Apply(WorkflowFeedback.Success("Supplier updated."));
            }
            else
            {
                Status.Apply(WorkflowFeedback.FromError(result.Error!));
            }
        });
    }

    private async void OnToggleActive(object sender, RoutedEventArgs args)
    {
        if ((sender as Button)?.Tag is not SupplierRow row)
        {
            return;
        }

        await RunAsync("change supplier availability", async () =>
        {
            Result result = row.IsActive
                ? await handler.DeactivateSupplierAsync(new DeactivateSupplierCommand(row.Id))
                : await handler.ActivateSupplierAsync(new ActivateSupplierCommand(row.Id));
            if (result.IsSuccess)
            {
                await LoadCoreAsync();
                Status.Apply(WorkflowFeedback.Success(row.IsActive ? "Supplier deactivated." : "Supplier activated."));
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

    private async Task LoadCoreAsync()
    {
        var suppliers = await repository.ListSuppliersAsync();
        allRows = suppliers.Select(supplier => new SupplierRow(supplier)).ToList();
        SuppliersList.ItemsSource = allRows;
        EmptyMessage.Visibility = allRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
        }
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        LoadingIndicator.IsActive = busy;
        LoadingIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ReloadButton.IsEnabled = !busy;
        AddButton.IsEnabled = !busy;
    }
}
