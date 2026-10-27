using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class StockRoomPage : Page
{
    private readonly PosOperatorSession session;
    private List<IngredientRow> allIngredientRows = [];
    private List<Ingredient> rawIngredients = [];
    private List<PosStaffOption> staffOptions = [];
    private List<PurchaseReceipt> rawReceipts = [];
    private Dictionary<EntityId, string> supplierNames = [];
    private List<StockMovement> recentMovements = [];

    private IngredientRow? selectedIngredientRow;
    private string activeSection = "ingredients";
    private bool isBusy;
    private bool compactLayout;

    public StockRoomPage()
    {
        InitializeComponent();
        session = App.Services.GetRequiredService<PosOperatorSession>();
        Loaded += OnLoaded;
    }

    public string CurrentSection => activeSection;

    public void SelectSection(string section)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        activeSection = section.ToLowerInvariant();
        UpdateActiveTabVisuals();
        SwitchSectionView();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadAllDataAsync();
        UpdateActiveTabVisuals();
        SwitchSectionView();
    }

    public async Task RefreshDataAsync() => await LoadAllDataAsync();

    private async Task LoadAllDataAsync()
    {
        if (isBusy) return;
        SetBusy(true);
        ShowFeedback(WorkflowFeedback.Loading("Syncing inventory, suppliers, receipts, and movements..."));

        try
        {
            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
            var movementReader = scope.ServiceProvider.GetRequiredService<IStockMovementReader>();
            var posHandler = scope.ServiceProvider.GetRequiredService<PosOrderHandler>();

            rawIngredients = (await repo.ListIngredientsAsync()).ToList();
            allIngredientRows = rawIngredients.Select(i => new IngredientRow(i)).ToList();

            var suppliers = await repo.ListSuppliersAsync();
            supplierNames = suppliers.ToDictionary(s => s.Id, s => s.Name);
            SuppliersListView.ItemsSource = suppliers.Select(s => new SupplierRow(s)).ToList();

            rawReceipts = (await repo.ListReceiptsAsync()).ToList();
            ReceiptsListView.ItemsSource = rawReceipts.Select(r => new ReceiptPresentationRow(
                r,
                supplierNames.TryGetValue(r.SupplierId, out var sName) ? sName : "Unknown Supplier"
            )).ToList();

            var staffResult = await posHandler.GetOptionsAsync();
            staffOptions = staffResult.Staff.ToList();
            DrawerOperatorBox.ItemsSource = staffOptions;
            DrawerOperatorBox.SelectedItem = staffOptions.FirstOrDefault(s => s.Id == session.Id)
                ?? staffOptions.FirstOrDefault();

            // Resolve selected ingredient
            if (selectedIngredientRow is not null)
            {
                var refreshed = allIngredientRows.FirstOrDefault(r => r.Id == selectedIngredientRow.Id);
                selectedIngredientRow = refreshed ?? allIngredientRows.FirstOrDefault();
            }
            else if (allIngredientRows.Count > 0)
            {
                selectedIngredientRow = allIngredientRows.First();
            }

            if (selectedIngredientRow is not null)
            {
                recentMovements = (await movementReader.ListRecentAsync(selectedIngredientRow.Id)).ToList();
            }
            else
            {
                recentMovements = [];
            }

            // Counts & Subtitle
            var lowCount = rawIngredients.Count(i => i.Status == StockStatus.LowStock);
            TabBalances.Content = $"BALANCES ({rawIngredients.Count})";
            TabLowStock.Content = $"LOW STOCK ({lowCount})";
            TabReceipts.Content = $"RECEIPTS ({rawReceipts.Count})";
            TabSuppliers.Content = $"SUPPLIERS ({suppliers.Count})";
            TabAdjustments.Content = "ADJUSTMENTS";

            SubtitleText.Text = $"{rawIngredients.Count} ingredients, {lowCount} below reorder point, {rawReceipts.Count(r => r.Status == PurchaseReceiptStatus.Draft)} open draft receipts.";
            SyncPillText.Text = $"SYNCED {DateTime.Now:HH:mm}";

            ApplyIngredientFilters();
            SelectIngredient(selectedIngredientRow);

            ShowFeedback(WorkflowFeedback.Ready("Stock room data refreshed."));
        }
        catch (Exception)
        {
            ShowFeedback(WorkflowFeedback.DatabaseFailure("load stock room data", "Check database connection and select Refresh."));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void UpdateActiveTabVisuals()
    {
        var accentBrush = (Brush)XamlApp.Current.Resources["ConsoleAccentBrush"];
        var groundBrush = (Brush)XamlApp.Current.Resources["ConsoleGroundBrush"];
        var panelBrush = (Brush)XamlApp.Current.Resources["ConsolePanelBrush"];

        var tabs = new[]
        {
            (TabBalances, "ingredients"),
            (TabLowStock, "lowstock"),
            (TabReceipts, "receipts"),
            (TabSuppliers, "suppliers"),
            (TabAdjustments, "adjustments"),
            (TabMovements, "movements")
        };

        foreach (var (btn, tag) in tabs)
        {
            bool isCurrent = activeSection == tag;
            btn.Background = isCurrent ? panelBrush : groundBrush;
            btn.BorderBrush = isCurrent ? accentBrush : (Brush)XamlApp.Current.Resources["ConsoleRuleBrush"];
            btn.BorderThickness = isCurrent ? new Thickness(0, 0, 0, 2) : new Thickness(0);
        }
    }

    private void SwitchSectionView()
    {
        BalancesView.Visibility = Visibility.Collapsed;
        ReceiptsView.Visibility = Visibility.Collapsed;
        SuppliersView.Visibility = Visibility.Collapsed;

        switch (activeSection)
        {
            case "ingredients":
            case "balances":
                BalancesView.Visibility = Visibility.Visible;
                LowStockOnlyCheck.IsChecked = false;
                DrawerPanel.Visibility = Visibility.Visible;
                break;

            case "lowstock":
                BalancesView.Visibility = Visibility.Visible;
                LowStockOnlyCheck.IsChecked = true;
                DrawerPanel.Visibility = Visibility.Visible;
                break;

            case "adjustments":
                BalancesView.Visibility = Visibility.Visible;
                DrawerPanel.Visibility = Visibility.Visible;
                DeltaInputBox.Focus(FocusState.Programmatic);
                break;

            case "receipts":
                ReceiptsView.Visibility = Visibility.Visible;
                DrawerPanel.Visibility = compactLayout ? Visibility.Collapsed : Visibility.Visible;
                break;

            case "suppliers":
                SuppliersView.Visibility = Visibility.Visible;
                DrawerPanel.Visibility = compactLayout ? Visibility.Collapsed : Visibility.Visible;
                break;

            default:
                BalancesView.Visibility = Visibility.Visible;
                DrawerPanel.Visibility = Visibility.Visible;
                break;
        }

        ApplyIngredientFilters();
    }

    private void ApplyIngredientFilters()
    {
        var query = SearchBox.Text?.Trim();
        var lowOnly = LowStockOnlyCheck.IsChecked == true || activeSection == "lowstock";
        var selectedUnitIdx = UnitFilterBox.SelectedIndex;

        var filtered = allIngredientRows.Where(row =>
        {
            if (!string.IsNullOrEmpty(query) && !row.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                return false;

            if (lowOnly && row.StatusDisplay != "Low stock")
                return false;

            if (selectedUnitIdx == 1 && row.BaseUnit != "g") return false;
            if (selectedUnitIdx == 2 && row.BaseUnit != "ml") return false;
            if (selectedUnitIdx == 3 && row.BaseUnit != "pcs") return false;

            return true;
        }).ToList();

        IngredientsListView.ItemsSource = filtered;
        BalancesCountText.Text = $"Showing {filtered.Count} of {allIngredientRows.Count}";
        BalancesEmptyText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SelectIngredient(IngredientRow? row)
    {
        selectedIngredientRow = row;
        IngredientsListView.SelectedItem = row;

        if (row is null)
        {
            DrawerIngredientTitle.Text = "No ingredient selected";
            DrawerBalanceValue.Text = "—";
            DrawerThresholdMeta.Text = "Select an ingredient from the list.";
            DrawerStatusText.Text = "NONE";
            DeltaUnitText.Text = "";
            DrawerUnitTag.Text = "UNIT";
            RecordAdjustmentButton.IsEnabled = false;
            IngredientMovementsList.ItemsSource = null;
            NoMovementsText.Visibility = Visibility.Visible;
            return;
        }

        DrawerIngredientTitle.Text = row.Name;
        DrawerBalanceValue.Text = row.BalanceDisplay;
        DrawerThresholdMeta.Text = $"Reorder at {row.ThresholdDisplay} · Unit: {row.BaseUnit}";
        DrawerStatusText.Text = row.StatusDisplay.ToUpperInvariant();
        DrawerUnitTag.Text = row.BaseUnit.ToUpperInvariant();
        DeltaUnitText.Text = row.BaseUnit;
        RecordAdjustmentButton.IsEnabled = !isBusy && row.IsActive;

        try
        {
            using var scope = App.Services.CreateScope();
            var reader = scope.ServiceProvider.GetRequiredService<IStockMovementReader>();
            recentMovements = (await reader.ListRecentAsync(row.Id)).ToList();
            IngredientMovementsList.ItemsSource = recentMovements.Select(m => new MovementPresentationRow(m, row.Name)).ToList();
            NoMovementsText.Visibility = recentMovements.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TabMovements.Content = $"MOVEMENTS ({recentMovements.Count})";
        }
        catch
        {
            NoMovementsText.Visibility = Visibility.Visible;
        }

        RecalculateProjection();
    }

    private void RecalculateProjection()
    {
        if (selectedIngredientRow is null)
        {
            ProjectedBalanceText.Text = "—";
            ProjectedStatusStamp.Visibility = Visibility.Collapsed;
            return;
        }

        var raw = rawIngredients.FirstOrDefault(i => i.Id == selectedIngredientRow.Id);
        if (raw is null) return;

        if (CatalogNumberInput.TryParse(DeltaInputBox.Text, out var delta))
        {
            var projected = raw.AvailableQuantity + delta;
            ProjectedBalanceText.Text = $"{projected:0.###} {raw.BaseUnit}";

            if (projected < 0)
            {
                ProjectedStatusStamp.Visibility = Visibility.Visible;
                ProjectedStatusText.Text = "NEGATIVE STOCK";
            }
            else if (projected <= raw.ReorderThreshold)
            {
                ProjectedStatusStamp.Visibility = Visibility.Visible;
                ProjectedStatusText.Text = "BELOW REORDER";
            }
            else
            {
                ProjectedStatusStamp.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            ProjectedBalanceText.Text = "—";
            ProjectedStatusStamp.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnRecordAdjustmentClick(object sender, RoutedEventArgs e)
    {
        if (selectedIngredientRow is null)
        {
            ShowFeedback(WorkflowFeedback.Validation("ingredient", "Select an ingredient first."));
            return;
        }

        if (!CatalogNumberInput.TryParse(DeltaInputBox.Text, out var delta) || delta == 0)
        {
            ShowFeedback(WorkflowFeedback.Validation("delta", "Enter a non-zero quantity change."));
            return;
        }

        var reason = ReasonInputBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            ShowFeedback(WorkflowFeedback.Validation("reason", "Adjustment reason is mandatory."));
            return;
        }

        var op = DrawerOperatorBox.SelectedItem as PosStaffOption ?? session.Operator;
        if (op is null)
        {
            ShowFeedback(WorkflowFeedback.Validation("operator", "Select an active staff operator."));
            return;
        }

        SetBusy(true);
        ShowFeedback(WorkflowFeedback.Loading("Recording adjustment..."));

        try
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>();
            var result = await handler.RecordStockAdjustmentAsync(new(selectedIngredientRow.Id, delta, reason, op.Id));

            if (result.IsSuccess)
            {
                DeltaInputBox.Text = string.Empty;
                ReasonInputBox.Text = string.Empty;
                await LoadAllDataAsync();
                ShowFeedback(WorkflowFeedback.Success($"Stock adjustment of {delta:+0.###;-0.###} {selectedIngredientRow.BaseUnit} recorded."));
            }
            else
            {
                ShowFeedback(WorkflowFeedback.FromError(result.Error!));
            }
        }
        catch (Exception)
        {
            ShowFeedback(WorkflowFeedback.DatabaseFailure("record stock adjustment", "Inspect history before retrying."));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnNewIngredientClick(object sender, RoutedEventArgs e)
    {
        var dialog = new IngredientDialog();
        dialog.XamlRoot = XamlRoot;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        SetBusy(true);
        try
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>();
            var result = await handler.CreateIngredientAsync(new(dialog.IngredientName, dialog.BaseUnit, dialog.AvailableQuantity, dialog.ReorderThreshold));
            if (result.IsSuccess)
            {
                await LoadAllDataAsync();
                ShowFeedback(WorkflowFeedback.Success($"Ingredient '{dialog.IngredientName}' created."));
            }
            else ShowFeedback(WorkflowFeedback.FromError(result.Error!));
        }
        catch { ShowFeedback(WorkflowFeedback.DatabaseFailure("create ingredient", "Retry after verifying connection.")); }
        finally { SetBusy(false); }
    }

    private async void OnReceiveDeliveryClick(object sender, RoutedEventArgs e)
    {
        var activeSuppliers = supplierNames.Values.OrderBy(n => n).ToList();
        var dialog = new NewReceiptDialog(activeSuppliers);
        dialog.XamlRoot = XamlRoot;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var supplierEntry = supplierNames.FirstOrDefault(kvp => kvp.Value == dialog.SupplierName);
        if (supplierEntry.Key is null)
        {
            ShowFeedback(WorkflowFeedback.Validation("supplier", "Select an active supplier."));
            return;
        }

        SetBusy(true);
        try
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>();
            var result = await handler.CreateDraftReceiptAsync(new(supplierEntry.Key, dialog.ReceiptNumber));
            if (result.IsSuccess)
            {
                SelectSection("receipts");
                await LoadAllDataAsync();
                ShowFeedback(WorkflowFeedback.Success($"Draft receipt #{dialog.ReceiptNumber} created."));
            }
            else ShowFeedback(WorkflowFeedback.FromError(result.Error!));
        }
        catch { ShowFeedback(WorkflowFeedback.DatabaseFailure("create draft receipt", "Verify database connection.")); }
        finally { SetBusy(false); }
    }

    private async void OnPostReceiptClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ReceiptPresentationRow row) return;

        var op = session.Operator ?? staffOptions.FirstOrDefault();
        if (op is null)
        {
            ShowFeedback(WorkflowFeedback.Validation("operator", "Active staff operator required to post receipt."));
            return;
        }

        SetBusy(true);
        try
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>();
            var result = await handler.PostReceiptAsync(new PostReceiptCommand(row.Id, op.Id));
            if (result.IsSuccess)
            {
                await LoadAllDataAsync();
                ShowFeedback(WorkflowFeedback.Success($"Purchase receipt #{row.ReceiptNumber} posted and inventory added."));
            }
            else ShowFeedback(WorkflowFeedback.FromError(result.Error!));
        }
        catch { ShowFeedback(WorkflowFeedback.DatabaseFailure("post purchase receipt", "Inspect receipt status before retrying.")); }
        finally { SetBusy(false); }
    }

    private async void OnAddSupplierClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SupplierDialog();
        dialog.XamlRoot = XamlRoot;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        SetBusy(true);
        try
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>();
            var result = await handler.CreateSupplierAsync(new(dialog.SupplierName, dialog.Phone, dialog.Email, dialog.Address));
            if (result.IsSuccess)
            {
                await LoadAllDataAsync();
                ShowFeedback(WorkflowFeedback.Success($"Supplier '{dialog.SupplierName}' created."));
            }
            else ShowFeedback(WorkflowFeedback.FromError(result.Error!));
        }
        catch { ShowFeedback(WorkflowFeedback.DatabaseFailure("create supplier", "Verify connection.")); }
        finally { SetBusy(false); }
    }

    private async void OnEditSupplierClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not SupplierRow row) return;

        using var scope = App.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        var supplier = await repo.FindSupplierAsync(row.Id);
        if (supplier is null)
        {
            ShowFeedback(WorkflowFeedback.FromError(Error.NotFound("supplier.not_found", "Supplier not found.")));
            return;
        }

        var dialog = new SupplierDialog(supplier);
        dialog.XamlRoot = XamlRoot;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        SetBusy(true);
        try
        {
            var handler = scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>();
            var result = await handler.UpdateSupplierAsync(new(supplier.Id, dialog.SupplierName, dialog.Phone, dialog.Email, dialog.Address));
            if (result.IsSuccess)
            {
                await LoadAllDataAsync();
                ShowFeedback(WorkflowFeedback.Success($"Supplier '{dialog.SupplierName}' updated."));
            }
            else ShowFeedback(WorkflowFeedback.FromError(result.Error!));
        }
        catch { ShowFeedback(WorkflowFeedback.DatabaseFailure("update supplier", "Verify connection.")); }
        finally { SetBusy(false); }
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            SelectSection(tag);
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await LoadAllDataAsync();

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => ApplyIngredientFilters();
    private void OnUnitFilterChanged(object sender, SelectionChangedEventArgs e) => ApplyIngredientFilters();
    private void OnLowStockFilterChecked(object sender, RoutedEventArgs e) => ApplyIngredientFilters();
    private void OnLowStockFilterUnchecked(object sender, RoutedEventArgs e) => ApplyIngredientFilters();

    private void OnIngredientSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IngredientsListView.SelectedItem is IngredientRow row)
        {
            SelectIngredient(row);
        }
    }

    private void OnRowAdjustClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is IngredientRow row)
        {
            SelectIngredient(row);
            DrawerPanel.Visibility = Visibility.Visible;
            DeltaInputBox.Focus(FocusState.Programmatic);
        }
    }

    private void OnReceiptSelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void OnDrawerCloseClick(object sender, RoutedEventArgs e)
    {
        if (compactLayout) DrawerPanel.Visibility = Visibility.Collapsed;
    }

    private void OnDeltaInputChanged(object sender, TextChangedEventArgs e) => RecalculateProjection();

    private void OnQuickChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string valStr } && decimal.TryParse(valStr, out var delta))
        {
            DeltaInputBox.Text = delta > 0 ? $"+{delta}" : $"{delta}";
            RecalculateProjection();
        }
    }

    private void OnDrawerOperatorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DrawerOperatorBox.SelectedItem is PosStaffOption staff)
        {
            session.Operator = staff;
            AuditActorNote.Text = $"Written against {staff.FullName}. The movement cannot be edited or deleted.";
        }
    }

    private void OnDismissStatus(object sender, RoutedEventArgs e) => StatusBanner.Visibility = Visibility.Collapsed;

    private void ShowFeedback(WorkflowFeedback feedback)
    {
        var message = string.IsNullOrWhiteSpace(feedback.DisplayMessage) ? feedback.Title : $"{feedback.Title}: {feedback.DisplayMessage}";
        StatusBanner.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
        StatusMessageText.Text = message;
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        RefreshButton.IsEnabled = !busy;
        NewIngredientButton.IsEnabled = !busy;
        ReceiveDeliveryButton.IsEnabled = !busy;
        RecordAdjustmentButton.IsEnabled = !busy && selectedIngredientRow?.IsActive == true;
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        compactLayout = e.NewSize.Width < 641;
        if (compactLayout)
        {
            ContentSplitGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(DrawerPanel, 0);
            DrawerPanel.Visibility = (activeSection == "adjustments") ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            ContentSplitGrid.ColumnDefinitions[1].Width = new GridLength(360, GridUnitType.Pixel);
            Grid.SetColumn(DrawerPanel, 1);
            DrawerPanel.Visibility = Visibility.Visible;
        }
    }
}

public sealed record ReceiptPresentationRow(PurchaseReceipt Receipt, string SupplierName)
{
    public EntityId Id => Receipt.Id;
    public string ReceiptNumber => Receipt.ReceiptNumber;
    public string ReceiptTitle => $"Receipt #{ReceiptNumber}";
    public string Status => Receipt.Status.ToString();
    public string LineCountDisplay => $"{Receipt.Lines.Count} item(s)";
    public bool CanPost => Receipt.Status == PurchaseReceiptStatus.Draft && Receipt.Lines.Count > 0;
}

public sealed record MovementPresentationRow(StockMovement Movement, string IngredientName)
{
    public string TimeDisplay => Movement.OccurredAt.Value.ToString("HH:mm");
    public string TypeDisplay => Movement.Type.ToString();
    public string DeltaDisplay => $"{Movement.QuantityDelta:+0.###;-0.###}";
    public string ReasonDisplay => $"{IngredientName}: {Movement.Reason}";
    public string ActorDisplay => $"Staff: {Movement.ActorId}";
    public Brush DeltaBrush => Movement.QuantityDelta >= 0
        ? (Brush)XamlApp.Current.Resources["ConsoleAccentTextBrush"]
        : (Brush)XamlApp.Current.Resources["ConsoleMutedInkBrush"];
}
