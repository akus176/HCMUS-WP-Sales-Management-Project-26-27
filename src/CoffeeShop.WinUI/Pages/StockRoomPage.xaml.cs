using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
    private Dictionary<EntityId, string> activeSupplierNames = [];
    private Dictionary<EntityId, Supplier> supplierDirectory = [];
    private List<StockMovement> allMovements = [];
    private List<HistoryFilterOption> historyFilters = [];
    private EntityId? selectedReceiptId;
    private List<StockMovement> recentMovements = [];
    private readonly Dictionary<EntityId, IReadOnlyList<StockMovement>> movementsByIngredient = [];

    private IngredientRow? selectedIngredientRow;
    private string activeSection = "ingredients";
    private bool isBusy;
    private bool compactLayout;
    private bool isInitialized;
    private bool drawerOpen = true;
    private bool isAdjusting;
    private bool restoringDraft;
    private bool selectingIngredient;
    private bool hasLoaded;
    private readonly StockAdjustmentDrafts adjustmentDrafts = new();
    private int selectedUnitIndex;
    private string sortColumn = "status";
    private bool sortDescending;

    public StockRoomPage()
    {
        InitializeComponent();
        session = App.Services.GetRequiredService<PosOperatorSession>();
        isInitialized = true;
        Loaded += OnLoaded;
    }

    public string CurrentSection => activeSection;

    public void SelectSection(string section)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        var entry = StockRoomInteraction.Resolve(section);
        activeSection = entry.Tab;
        isAdjusting = false;
        if (section.Equals("lowstock", StringComparison.OrdinalIgnoreCase)) LowStockOnlyCheck.IsChecked = true;
        else if (section is "ingredients" or "balances") LowStockOnlyCheck.IsChecked = false;
        UpdateActiveTabVisuals();
        SwitchSectionView();
        if (entry.Adjust) OnBeginAdjustmentClick(this, new RoutedEventArgs());
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConfigureLayout(ActualWidth, ActualHeight);
        if (!hasLoaded) await LoadAllDataAsync();
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
            var api = App.IsApiMode ? App.Services.GetRequiredService<ApiInventorySource>() : null;
            List<Supplier> suppliers;
            if (api is null)
            {
                var repo = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
                rawIngredients = (await repo.ListIngredientsAsync()).ToList();
                suppliers = (await repo.ListSuppliersAsync()).ToList();
                rawReceipts = (await repo.ListReceiptsAsync()).ToList();
                var movementReader = scope.ServiceProvider.GetRequiredService<IStockMovementReader>();
                movementsByIngredient.Clear();
                foreach (var ingredient in rawIngredients)
                    movementsByIngredient[ingredient.Id] = await movementReader.ListRecentAsync(ingredient.Id);
                allMovements = (await movementReader.ListRecentForAllAsync()).ToList();
                staffOptions = (await scope.ServiceProvider.GetRequiredService<PosOrderHandler>().GetOptionsAsync()).Staff.ToList();
            }
            else
            {
                rawIngredients = (await api.ListIngredientsAsync()).ToList();
                suppliers = (await api.ListSuppliersAsync()).ToList();
                rawReceipts = (await api.ListReceiptsAsync()).ToList();
                allMovements = (await api.ListMovementsAsync()).ToList();
                movementsByIngredient.Clear();
                foreach (var ingredient in rawIngredients)
                    movementsByIngredient[ingredient.Id] = allMovements.Where(movement => movement.IngredientId == ingredient.Id)
                        .OrderByDescending(movement => movement.OccurredAt.Value).ToArray();
                staffOptions = session.Staff is { } staff ? [new(new(staff.Id), staff.FullName)] : [];
            }
            var ingredientsById = rawIngredients.ToDictionary(ingredient => ingredient.Id);

            supplierDirectory = suppliers.ToDictionary(supplier => supplier.Id);
            supplierNames = suppliers.ToDictionary(s => s.Id, s => s.Name);
            activeSupplierNames = suppliers.Where(s => s.IsActive).ToDictionary(s => s.Id, s => s.Name);

            ReceiptsEmptyText.Visibility = rawReceipts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            var receiptCounts = rawReceipts
                .GroupBy(receipt => receipt.SupplierId)
                .ToDictionary(
                    group => group.Key,
                    group => (Total: group.Count(), Posted: group.Count(receipt => receipt.Status == PurchaseReceiptStatus.Posted)));
            SuppliersListView.ItemsSource = suppliers.Select(supplier =>
            {
                var counts = receiptCounts.GetValueOrDefault(supplier.Id);
                return new SupplierRow(supplier, counts.Total, counts.Posted);
            }).ToList();
            SuppliersEmptyText.Visibility = suppliers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            var receiptRows = rawReceipts.Select(receipt =>
            {
                var supplierName = supplierNames.GetValueOrDefault(receipt.SupplierId, "Unknown supplier");
                var contact = supplierDirectory.TryGetValue(receipt.SupplierId, out var supplier)
                    ? string.Join(" · ", new[] { supplier.Phone, supplier.Email }.Where(value => !string.IsNullOrWhiteSpace(value)))
                    : string.Empty;
                return new ReceiptPresentationRow(receipt, supplierName, contact, ingredientsById);
            }).ToList();
            ReceiptsListView.ItemsSource = receiptRows;
            var receiptSelection = receiptRows.FirstOrDefault(row => row.Id == selectedReceiptId) ?? receiptRows.FirstOrDefault();
            selectedReceiptId = receiptSelection?.Id;
            ReceiptsListView.SelectedItem = receiptSelection;
            ShowReceiptDetail(receiptSelection);

            DrawerOperatorBox.ItemsSource = staffOptions;
            DrawerOperatorBox.SelectedItem = staffOptions.FirstOrDefault(s => s.Id == session.Id)
                ?? staffOptions.FirstOrDefault();

            allIngredientRows = rawIngredients.Select(i =>
            {
                var latest = movementsByIngredient[i.Id].FirstOrDefault();
                return new IngredientRow(i, latest, staffOptions.FirstOrDefault(s => s.Id == latest?.ActorId)?.FullName);
            }).ToList();

            var previousHistoryFilter = (HistoryIngredientBox.SelectedItem as HistoryFilterOption)?.IngredientId;
            historyFilters =
            [
                new("All ingredients", null),
                .. allIngredientRows.Select(row => new HistoryFilterOption(row.Name, row.Id)),
            ];
            HistoryIngredientBox.ItemsSource = historyFilters;
            HistoryIngredientBox.SelectedItem = historyFilters.FirstOrDefault(option => option.IngredientId == previousHistoryFilter)
                ?? historyFilters[0];
            ApplyHistoryFilter();

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
                recentMovements = movementsByIngredient[selectedIngredientRow.Id].ToList();
            }
            else
            {
                recentMovements = [];
            }

            // Counts & Subtitle
            var lowCount = rawIngredients.Count(i => i.IsActive && i.Status == StockStatus.LowStock);
            LowStockOnlyCheck.Content = lowCount > 0 ? $"Low stock only ({lowCount})" : "Low stock only";
            SetTabLabel(TabBalances, "Inventory", rawIngredients.Count);
            SetTabLabel(TabReceipts, "Receipts", rawReceipts.Count);
            SetTabLabel(TabSuppliers, "Suppliers", suppliers.Count);
            SetTabLabel(TabMovements, "History");

            SubtitleText.Text = $"{rawIngredients.Count} ingredients · {lowCount} low on stock · {rawReceipts.Count(r => r.Status == PurchaseReceiptStatus.Draft)} draft receipts";
            SyncPillText.Text = $"Updated {DateTime.Now:HH:mm}";
            var lastMovement = movementsByIngredient.Values.SelectMany(items => items).MaxBy(m => m.OccurredAt.Value);
            if (lastMovement is not null)
            {
                var age = DateTimeOffset.UtcNow - lastMovement.OccurredAt.Value;
                var elapsed = age.TotalDays >= 1 ? $"{(int)age.TotalDays}D" : $"{Math.Max(0, (int)age.TotalMinutes)} MIN";
                EyebrowText.Text = $"MANAGE  /  INVENTORY  /  LAST MOVEMENT {elapsed} AGO";
            }

            ApplyIngredientFilters();
            SelectIngredient(selectedIngredientRow);
            hasLoaded = true;

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

        var tabs = new[]
        {
            (TabBalances, "ingredients"),
            (TabReceipts, "receipts"),
            (TabSuppliers, "suppliers"),
            (TabMovements, "movements")
        };

        foreach (var (btn, tag) in tabs)
        {
            bool isCurrent = activeSection == tag;
            btn.IsChecked = isCurrent;
            btn.Background = groundBrush;
            btn.Foreground = (Brush)XamlApp.Current.Resources[isCurrent ? "ConsoleInkBrush" : "ConsoleMutedInkBrush"];
            btn.BorderBrush = isCurrent ? accentBrush : (Brush)XamlApp.Current.Resources["ConsoleRuleBrush"];
            btn.BorderThickness = isCurrent ? new Thickness(0, 0, 0, 1) : new Thickness(0);
        }
    }

    private void SwitchSectionView()
    {
        drawerOpen = true;
        if (!compactLayout)
        {
            ContentSplitGrid.ColumnDefinitions[1].Width = new GridLength(360);
            ContentSplitGrid.ColumnSpacing = 24;
        }
        BalancesView.Visibility = Visibility.Collapsed;
        ReceiptsView.Visibility = Visibility.Collapsed;
        SuppliersView.Visibility = Visibility.Collapsed;
        MovementsView.Visibility = Visibility.Collapsed;

        switch (activeSection)
        {
            case "ingredients":
            case "balances":
                BalancesView.Visibility = Visibility.Visible;
                DrawerPanel.Visibility = Visibility.Visible;
                break;

            case "receipts":
                ReceiptsView.Visibility = Visibility.Visible;
                DrawerPanel.Visibility = compactLayout ? Visibility.Collapsed : Visibility.Visible;
                break;

            case "suppliers":
                SuppliersView.Visibility = Visibility.Visible;
                DrawerPanel.Visibility = compactLayout ? Visibility.Collapsed : Visibility.Visible;
                break;

            case "movements":
                MovementsView.Visibility = Visibility.Visible;
                DrawerPanel.Visibility = compactLayout ? Visibility.Collapsed : Visibility.Visible;
                break;
        }

        ApplyIngredientFilters();
        if (activeSection == "movements") SelectHistoryFilter(null);
        NewIngredientButton.Visibility = activeSection == "ingredients" && session.HasPermission("inventory.manage") ? Visibility.Visible : Visibility.Collapsed;
        ReceiveDeliveryButton.Visibility = (activeSection is "ingredients" or "receipts") && session.HasPermission("inventory.receive") ? Visibility.Visible : Visibility.Collapsed;
        NewSupplierButton.Visibility = activeSection == "suppliers" && session.HasPermission("inventory.manage") ? Visibility.Visible : Visibility.Collapsed;
        UpdateEditor();
    }

    private void ApplyIngredientFilters()
    {
        // XAML selection events can run before the remaining named controls exist.
        if (!isInitialized) return;
        var query = SearchBox.Text?.Trim();
        var lowOnly = LowStockOnlyCheck.IsChecked == true;
        var selectedUnitIdx = selectedUnitIndex;
        var hasFilters = !string.IsNullOrWhiteSpace(query) || lowOnly || selectedUnitIdx > 0;

        var filtered = allIngredientRows.Where(row =>
        {
            if (!string.IsNullOrEmpty(query) && !row.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                return false;

            if (lowOnly && (!row.IsActive || row.StatusDisplay != "Low stock"))
                return false;

            if (selectedUnitIdx == 1 && row.BaseUnit != "g") return false;
            if (selectedUnitIdx == 2 && row.BaseUnit != "ml") return false;
            if (selectedUnitIdx == 3 && row.BaseUnit != "pcs") return false;

            return true;
        });

        var ordered = SortRows(filtered).ToList();
        IngredientsListView.ItemsSource = ordered;
        BalancesCountText.Text = hasFilters ? $"{ordered.Count} of {allIngredientRows.Count}" : $"{allIngredientRows.Count} items";
        FooterCountText.Text = (hasFilters
            ? $"{ordered.Count} of {allIngredientRows.Count} ingredients"
            : $"{allIngredientRows.Count} ingredients") + (ordered.Count > 8 ? " · Scroll to see more" : "");
        ClearFiltersButton.Visibility = hasFilters ? Visibility.Visible : Visibility.Collapsed;
        BalancesEmptyState.Visibility = ordered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BalancesEmptyClearButton.Visibility = hasFilters ? Visibility.Visible : Visibility.Collapsed;
        BalancesEmptyText.Text = allIngredientRows.Count == 0
            ? "No ingredients yet. Create one to start tracking stock."
            : hasFilters
                ? "No ingredients match these filters."
                : "No ingredients to show.";
        var visibleSelection = ordered.FirstOrDefault(row => row.Id == selectedIngredientRow?.Id) ?? ordered.FirstOrDefault();
        if (activeSection == "ingredients") SelectIngredient(visibleSelection);
        UpdateSortHeaders();
    }

    private IEnumerable<IngredientRow> SortRows(IEnumerable<IngredientRow> rows)
    {
        var byName = StringComparer.CurrentCultureIgnoreCase;
        return sortColumn switch
        {
            "ingredient" => sortDescending
                ? rows.OrderByDescending(row => row.Name, byName)
                : rows.OrderBy(row => row.Name, byName),
            "onhand" => sortDescending
                ? rows.OrderByDescending(row => row.AvailableQuantity).ThenBy(row => row.Name, byName)
                : rows.OrderBy(row => row.AvailableQuantity).ThenBy(row => row.Name, byName),
            "reorder" => sortDescending
                ? rows.OrderByDescending(row => row.ReorderThreshold).ThenBy(row => row.Name, byName)
                : rows.OrderBy(row => row.ReorderThreshold).ThenBy(row => row.Name, byName),
            "movement" => sortDescending
                ? rows.OrderBy(row => row.LastMovementAt is null).ThenByDescending(row => row.LastMovementAt ?? DateTimeOffset.MinValue).ThenBy(row => row.Name, byName)
                : rows.OrderBy(row => row.LastMovementAt is null).ThenBy(row => row.LastMovementAt ?? DateTimeOffset.MinValue).ThenBy(row => row.Name, byName),
            _ => sortDescending
                ? rows.OrderByDescending(row => row.StatusRank).ThenBy(row => row.Name, byName)
                : rows.OrderBy(row => row.StatusRank).ThenBy(row => row.Name, byName),
        };
    }

    private void OnSortHeaderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string column }) return;
        if (sortColumn == column)
        {
            sortDescending = !sortDescending;
        }
        else
        {
            sortColumn = column;
            sortDescending = false;
        }
        ApplyIngredientFilters();
    }

    private void UpdateSortHeaders()
    {
        if (!isInitialized) return;
        var accent = (Brush)XamlApp.Current.Resources["ConsoleAccentTextBrush"];
        var muted = (Brush)XamlApp.Current.Resources["ConsoleMutedInkBrush"];

        void Update(Button button, TextBlock indicator, string column, string label)
        {
            var active = sortColumn == column;
            indicator.Text = active ? (sortDescending ? "↓" : "↑") : "↕";
            indicator.Foreground = active ? accent : muted;
            AutomationProperties.SetName(button, active
                ? $"{label}, sorted {(sortDescending ? "descending" : "ascending")}"
                : label);
        }

        Update(SortIngredientButton, SortIngredientIndicator, "ingredient", "Sort ingredients by name");
        Update(SortOnHandButton, SortOnHandIndicator, "onhand", "Sort ingredients by quantity on hand");
        Update(SortReorderButton, SortReorderIndicator, "reorder", "Sort ingredients by reorder threshold");
        Update(SortStatusButton, SortStatusIndicator, "status", "Sort ingredients by stock status");
        Update(LastMovementHeaderButton, SortMovementIndicator, "movement", "Sort ingredients by last movement");
    }

    private void SelectIngredient(IngredientRow? row)
    {
        if (selectingIngredient) return;
        selectingIngredient = true;
        try
        {
            var changed = row?.Id != selectedIngredientRow?.Id;
            if (changed && selectedIngredientRow is { } previous)
                adjustmentDrafts.Remember(previous.Id, DeltaInputBox.Text, ReasonInputBox.Text);
            selectedIngredientRow = row;
            IngredientsListView.SelectedItem = row;
            if (changed)
            {
                if (row is not null) drawerOpen = true;
                isAdjusting = false;
                var draft = row is null ? new StockAdjustmentDraft() : adjustmentDrafts.For(row.Id);
                restoringDraft = true;
                DeltaInputBox.Text = draft.Quantity;
                ReasonInputBox.Text = draft.Reason;
                restoringDraft = false;
                DrawerScrollViewer.ChangeView(null, 0, null, true);
            }

            if (row is null)
            {
                DrawerIngredientTitle.Text = "No ingredient selected";
                DrawerBalanceValue.Text = "—";
                DrawerBalanceUnit.Text = "";
                DrawerThresholdMeta.Text = "Select an ingredient from the list.";
                DrawerStatusText.Text = "NONE";
                DeltaUnitText.Text = "";
                DrawerUnitTag.Text = "UNIT";
                RecordAdjustmentButton.IsEnabled = false;
                IngredientMovementsList.ItemsSource = null;
                NoMovementsText.Visibility = Visibility.Visible;
                UpdateEditor();
                return;
            }

            DrawerIngredientTitle.Text = row.Name;
            DrawerBalanceValue.Text = row.BalanceNumber;
            DrawerBalanceUnit.Text = " " + row.BaseUnit;
            DrawerThresholdMeta.Text = $"Reorder threshold {row.ThresholdDisplay}";
            DrawerStatusText.Text = !row.IsActive ? "INACTIVE" : row.IsLowStock ? "LOW STOCK" : "STOCK OK";
            DrawerStatusStamp.Background = (Brush)XamlApp.Current.Resources[
                !row.IsActive ? "ConsoleRaisedGroundBrush" : row.IsLowStock ? "ConsoleConfirmedBrush" : "ConsoleReadyBrush"];
            DrawerUnitTag.Text = $"Base unit · {row.BaseUnit}";
            DeltaUnitText.Text = row.BaseUnit;

            recentMovements = movementsByIngredient.TryGetValue(row.Id, out var movements) ? movements.ToList() : [];
            var movementRows = recentMovements.Select(m => new MovementPresentationRow(m, row.Name, row.BaseUnit,
                staffOptions.FirstOrDefault(s => s.Id == m.ActorId)?.FullName)).ToList();
            IngredientMovementsList.ItemsSource = movementRows;
            NoMovementsText.Visibility = recentMovements.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            RecalculateProjection();
            UpdateEditor();

        }
        finally { selectingIngredient = false; }
    }

    private void RecalculateProjection()
    {
        if (!isInitialized) return;
        UpdateActorDisplay();
        if (restoringDraft) return;
        if (selectedIngredientRow is { } selected) adjustmentDrafts.Remember(selected.Id, DeltaInputBox.Text, ReasonInputBox.Text);
        var ingredient = rawIngredients.FirstOrDefault(item => item.Id == selectedIngredientRow?.Id);
        var preview = StockAdjustmentPreview.Create(ingredient, DeltaInputBox.Text,
            DrawerOperatorBox.SelectedItem is PosStaffOption || session.Operator is not null);
        RecordAdjustmentButton.IsEnabled = !isBusy && preview.CanSubmit;
        AdjustmentHintText.Text = preview.Hint;
        BeginAdjustmentButton.IsEnabled = !isBusy && selectedIngredientRow?.IsActive == true;
        if (selectedIngredientRow is null)
        {
            ProjectedBalanceText.Text = "—";
            ProjectedStatusStamp.Visibility = Visibility.Collapsed;
            return;
        }

        var raw = rawIngredients.FirstOrDefault(i => i.Id == selectedIngredientRow.Id);
        if (raw is null) return;

        if (CatalogNumberInput.TryParse(DeltaInputBox.Text, out var delta) && preview.Balance is { } projected)
        {
            RecordAdjustmentButton.Content = "Save adjustment";
            ProjectedBalanceText.Text = $"{projected:#,0.###} {raw.BaseUnit}";

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
            RecordAdjustmentButton.Content = "Save adjustment";
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
        if (string.IsNullOrWhiteSpace(reason)) reason = "Manual stock adjustment";

        var op = App.IsApiMode ? staffOptions.FirstOrDefault(s => s.Id == session.Id)
            : DrawerOperatorBox.SelectedItem as PosStaffOption ?? session.Operator;
        if (op is null)
        {
            ShowFeedback(WorkflowFeedback.Validation("operator", "Select an active staff operator."));
            return;
        }

        SetBusy(true);
        ShowFeedback(WorkflowFeedback.Loading("Recording adjustment..."));

        try
        {
            if (App.IsApiMode)
            {
                await App.Services.GetRequiredService<ApiInventorySource>().AdjustAsync(selectedIngredientRow.Id, delta, reason);
            }
            else
            {
                using var scope = App.Services.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>()
                    .RecordStockAdjustmentAsync(new(selectedIngredientRow.Id, delta, reason, op.Id));
                if (result.IsFailure) { ShowFeedback(WorkflowFeedback.FromError(result.Error!)); return; }
            }
            adjustmentDrafts.Clear(selectedIngredientRow.Id);
            DeltaInputBox.Text = string.Empty;
            ReasonInputBox.Text = string.Empty;
            isAdjusting = false;
            await LoadAllDataAsync();
            ShowFeedback(WorkflowFeedback.Success($"Stock adjustment of {delta:+0.###;-0.###} {selectedIngredientRow.BaseUnit} recorded."));
        }
        catch (ApiException exception) { ShowApiFailure("record stock adjustment", exception); }
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
            if (App.IsApiMode)
            {
                await App.Services.GetRequiredService<ApiInventorySource>().CreateIngredientAsync(
                    dialog.IngredientName, dialog.BaseUnit, dialog.AvailableQuantity, dialog.ReorderThreshold);
            }
            else
            {
                using var scope = App.Services.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>().CreateIngredientAsync(
                    new(dialog.IngredientName, dialog.BaseUnit, dialog.AvailableQuantity, dialog.ReorderThreshold));
                if (result.IsFailure) { ShowFeedback(WorkflowFeedback.FromError(result.Error!)); return; }
            }
            await LoadAllDataAsync();
            ShowFeedback(WorkflowFeedback.Success($"Ingredient '{dialog.IngredientName}' created."));
        }
        catch (ApiException exception) { ShowApiFailure("create ingredient", exception); }
        catch { ShowFeedback(WorkflowFeedback.DatabaseFailure("create ingredient", "Retry after verifying connection.")); }
        finally { SetBusy(false); }
    }

    private async void OnReceiveDeliveryClick(object sender, RoutedEventArgs e)
    {
        if (activeSupplierNames.Count == 0)
        {
            SelectSection("suppliers");
            ShowFeedback(WorkflowFeedback.Validation("supplier", "Add an active supplier before receiving a delivery."));
            return;
        }
        var activeSuppliers = activeSupplierNames.Values.OrderBy(n => n).ToList();
        var dialog = new NewReceiptDialog(activeSuppliers);
        dialog.XamlRoot = XamlRoot;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var supplierEntry = activeSupplierNames.FirstOrDefault(kvp => kvp.Value == dialog.SupplierName);
        if (supplierEntry.Key is null)
        {
            ShowFeedback(WorkflowFeedback.Validation("supplier", "Select an active supplier."));
            return;
        }

        SetBusy(true);
        try
        {
            if (App.IsApiMode)
            {
                await App.Services.GetRequiredService<ApiInventorySource>().CreateReceiptAsync(supplierEntry.Key, dialog.ReceiptNumber);
            }
            else
            {
                using var scope = App.Services.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>()
                    .CreateDraftReceiptAsync(new(supplierEntry.Key, dialog.ReceiptNumber));
                if (result.IsFailure) { ShowFeedback(WorkflowFeedback.FromError(result.Error!)); return; }
            }
            SelectSection("receipts");
            await LoadAllDataAsync();
            ShowFeedback(WorkflowFeedback.Success($"Draft receipt #{dialog.ReceiptNumber} created."));
        }
        catch (ApiException exception) { ShowApiFailure("create purchase receipt", exception); }
        catch { ShowFeedback(WorkflowFeedback.DatabaseFailure("create draft receipt", "Verify database connection.")); }
        finally { SetBusy(false); }
    }

    private async void OnPostReceiptClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ReceiptPresentationRow row) return;

        var op = App.IsApiMode ? staffOptions.FirstOrDefault(s => s.Id == session.Id)
            : session.Operator ?? staffOptions.FirstOrDefault();
        if (op is null)
        {
            ShowFeedback(WorkflowFeedback.Validation("operator", "Active staff operator required to post receipt."));
            return;
        }

        SetBusy(true);
        try
        {
            if (App.IsApiMode)
            {
                await App.Services.GetRequiredService<ApiInventorySource>().PostReceiptAsync(row.Id);
            }
            else
            {
                using var scope = App.Services.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>()
                    .PostReceiptAsync(new PostReceiptCommand(row.Id, op.Id));
                if (result.IsFailure) { ShowFeedback(WorkflowFeedback.FromError(result.Error!)); return; }
            }
            await LoadAllDataAsync();
            ShowFeedback(WorkflowFeedback.Success($"Purchase receipt #{row.ReceiptNumber} posted and inventory added."));
        }
        catch (ApiException exception) { ShowApiFailure("post purchase receipt", exception); }
        catch { ShowFeedback(WorkflowFeedback.DatabaseFailure("post purchase receipt", "Inspect receipt status before retrying.")); }
        finally { SetBusy(false); }
    }

    private async void OnAddReceiptLineClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ReceiptPresentationRow row || row.Receipt.Status != PurchaseReceiptStatus.Draft) return;
        var activeIngredients = rawIngredients.Where(ingredient => ingredient.IsActive).OrderBy(ingredient => ingredient.Name).ToArray();
        if (activeIngredients.Length == 0)
        {
            ShowFeedback(WorkflowFeedback.Validation("ingredient", "Create an active ingredient before adding a receipt line."));
            return;
        }
        var dialog = new ReceiptLineDialog(activeIngredients) { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || dialog.SelectedIngredient is not { } ingredient) return;

        SetBusy(true);
        try
        {
            if (App.IsApiMode)
                await App.Services.GetRequiredService<ApiInventorySource>().AddReceiptLineAsync(row.Id, ingredient.Id, dialog.Quantity, dialog.UnitCost);
            else
            {
                using var scope = App.Services.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>().AddReceiptLineAsync(
                    new AddReceiptLineCommand(row.Id, ingredient.Id, dialog.Quantity, dialog.UnitCost));
                if (result.IsFailure) { ShowFeedback(WorkflowFeedback.FromError(result.Error!)); return; }
            }
            await LoadAllDataAsync();
            ShowFeedback(WorkflowFeedback.Success($"Added {dialog.Quantity:#,0.###} {ingredient.BaseUnit} of {ingredient.Name} to receipt #{row.ReceiptNumber}."));
        }
        catch (ApiException exception) { ShowApiFailure("add purchase receipt line", exception); }
        catch { ShowFeedback(WorkflowFeedback.DatabaseFailure("add purchase receipt line", "Reload the receipt before retrying.")); }
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
            if (App.IsApiMode)
            {
                await App.Services.GetRequiredService<ApiInventorySource>().CreateSupplierAsync(
                    dialog.SupplierName, dialog.Phone, dialog.Email, dialog.Address);
            }
            else
            {
                using var scope = App.Services.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>().CreateSupplierAsync(
                    new(dialog.SupplierName, dialog.Phone, dialog.Email, dialog.Address));
                if (result.IsFailure) { ShowFeedback(WorkflowFeedback.FromError(result.Error!)); return; }
            }
            await LoadAllDataAsync();
            ShowFeedback(WorkflowFeedback.Success($"Supplier '{dialog.SupplierName}' created."));
        }
        catch (ApiException exception) { ShowApiFailure("create supplier", exception); }
        catch { ShowFeedback(WorkflowFeedback.DatabaseFailure("create supplier", "Verify connection.")); }
        finally { SetBusy(false); }
    }

    private async void OnEditSupplierClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not SupplierRow row) return;

        using var scope = App.Services.CreateScope();
        var supplier = App.IsApiMode
            ? await App.Services.GetRequiredService<ApiInventorySource>().FindSupplierAsync(row.Id)
            : await scope.ServiceProvider.GetRequiredService<IInventoryRepository>().FindSupplierAsync(row.Id);
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
            if (App.IsApiMode)
            {
                await App.Services.GetRequiredService<ApiInventorySource>().UpdateSupplierAsync(
                    supplier.Id, dialog.SupplierName, dialog.Phone, dialog.Email, dialog.Address);
            }
            else
            {
                var result = await scope.ServiceProvider.GetRequiredService<InventoryCommandHandler>().UpdateSupplierAsync(
                    new(supplier.Id, dialog.SupplierName, dialog.Phone, dialog.Email, dialog.Address));
                if (result.IsFailure) { ShowFeedback(WorkflowFeedback.FromError(result.Error!)); return; }
            }
            await LoadAllDataAsync();
            ShowFeedback(WorkflowFeedback.Success($"Supplier '{dialog.SupplierName}' updated."));
        }
        catch (ApiException exception) { ShowApiFailure("update supplier", exception); }
        catch { ShowFeedback(WorkflowFeedback.DatabaseFailure("update supplier", "Verify connection.")); }
        finally { SetBusy(false); }
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: string tag })
        {
            activeSection = StockRoomInteraction.Resolve(tag).Tab;
            isAdjusting = false;
            UpdateActiveTabVisuals();
            SwitchSectionView();
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await LoadAllDataAsync();

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => ApplyIngredientFilters();
    private void OnUnitFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string tag } item) return;
        selectedUnitIndex = int.Parse(tag);
        UnitFilterBox.Content = item.Text + " ▾";
        ApplyIngredientFilters();
    }
    private void OnLowStockFilterChecked(object sender, RoutedEventArgs e) => ApplyIngredientFilters();
    private void OnLowStockFilterUnchecked(object sender, RoutedEventArgs e)
    {
        ApplyIngredientFilters();
    }

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
            SelectSection("adjustments");
            DrawerPanel.Visibility = Visibility.Visible;
            DeltaInputBox.Focus(FocusState.Programmatic);
        }
    }

    private void OnReceiptSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = ReceiptsListView.SelectedItem as ReceiptPresentationRow;
        selectedReceiptId = row?.Id;
        ShowReceiptDetail(row);
    }

    private void ShowReceiptDetail(ReceiptPresentationRow? row)
    {
        ReceiptDetailPanel.DataContext = row;
        ReceiptDetailPanel.Visibility = row is null ? Visibility.Collapsed : Visibility.Visible;
        UpdateReceiptLayout();
    }

    private void UpdateReceiptLayout()
    {
        if (!isInitialized) return;
        var hasDetail = ReceiptDetailPanel.Visibility == Visibility.Visible;
        ReceiptHeaderHint.Visibility = compactLayout ? Visibility.Collapsed : Visibility.Visible;
        ReceiptsBodyGrid.ColumnSpacing = compactLayout ? 0 : 24;
        if (compactLayout)
        {
            ReceiptsBodyGrid.ColumnDefinitions[1].Width = new GridLength(0);
            ReceiptsBodyGrid.RowDefinitions[1].Height = hasDetail ? GridLength.Auto : new GridLength(0);
            Grid.SetRow(ReceiptDetailPanel, 1);
            Grid.SetColumn(ReceiptDetailPanel, 0);
            Grid.SetColumnSpan(ReceiptDetailPanel, 2);
            Grid.SetColumnSpan(ReceiptListPanel, 2);
            ReceiptListPanel.MaxHeight = hasDetail ? 300 : double.PositiveInfinity;
            ReceiptDetailPanel.Margin = new Thickness(0, 12, 0, 0);
            ReceiptsView.RowDefinitions[1].Height = GridLength.Auto;
        }
        else
        {
            ReceiptsBodyGrid.ColumnDefinitions[1].Width = hasDetail ? new GridLength(360) : new GridLength(0);
            ReceiptsBodyGrid.RowDefinitions[1].Height = new GridLength(0);
            Grid.SetRow(ReceiptDetailPanel, 0);
            Grid.SetColumn(ReceiptDetailPanel, 1);
            Grid.SetColumnSpan(ReceiptDetailPanel, 1);
            Grid.SetColumnSpan(ReceiptListPanel, 1);
            ReceiptListPanel.MaxHeight = double.PositiveInfinity;
            ReceiptDetailPanel.Margin = new Thickness(0);
            ReceiptsView.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
        }
    }

    private void OnDrawerCloseClick(object sender, RoutedEventArgs e)
    {
        if (isAdjusting) { isAdjusting = false; UpdateEditor(); return; }
        if (compactLayout) SelectSection("ingredients");
        else
        {
            drawerOpen = false;
            DrawerPanel.Visibility = Visibility.Collapsed;
            ContentSplitGrid.ColumnDefinitions[1].Width = new GridLength(0);
            ContentSplitGrid.ColumnSpacing = 0;
        }
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
        if (App.IsApiMode) return;
        if (DrawerOperatorBox.SelectedItem is PosStaffOption staff)
        {
            session.Operator = staff;
            AuditActorNote.Text = "This movement cannot be edited or deleted after saving.";
            RecalculateProjection();
        }
    }

    private void UpdateActorDisplay()
    {
        if (!isInitialized) return;
        var actor = App.IsApiMode && session.Staff is { } staff
            ? new PosStaffOption(new EntityId(staff.Id), staff.FullName)
            : DrawerOperatorBox.SelectedItem as PosStaffOption ?? session.Operator;
        ActorDisplayText.Text = actor is null
            ? "Choose staff operator · change"
            : $"{actor.FullName} · change";
        AutomationProperties.SetHelpText(ActorButton, actor is null
            ? "Select the staff operator recorded on this stock movement."
            : $"Current operator {actor.FullName}. Select the staff operator recorded on this stock movement.");
    }

    private void OnDismissStatus(object sender, RoutedEventArgs e) => StatusBanner.Visibility = Visibility.Collapsed;

    private void ShowFeedback(WorkflowFeedback feedback)
    {
        var message = string.IsNullOrWhiteSpace(feedback.DisplayMessage) ? feedback.Title : $"{feedback.Title}: {feedback.DisplayMessage}";
        StatusBanner.Visibility = feedback.Kind == WorkflowFeedbackKind.Ready || string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
        StatusMessageText.Text = message;
    }

    private void ShowApiFailure(string operation, ApiException exception)
    {
        if (exception.Kind is ApiErrorKind.Validation or ApiErrorKind.Conflict or ApiErrorKind.NotFound or ApiErrorKind.Forbidden)
        {
            var kind = exception.Kind switch
            {
                ApiErrorKind.Validation => WorkflowFeedbackKind.Validation,
                ApiErrorKind.Conflict => WorkflowFeedbackKind.Conflict,
                _ => WorkflowFeedbackKind.Error
            };
            ShowFeedback(new(kind, exception.Title, exception.Detail,
                exception.Errors.SelectMany(group => group.Value.Select(message => new ErrorDetail(group.Key, message))).ToArray(),
                exception.Kind == ApiErrorKind.Conflict ? "Reload the record before you try again." : null));
            return;
        }

        ShowFeedback(new(WorkflowFeedbackKind.DatabaseError, "Service unavailable",
            $"Could not {operation}. No local database was accessed.", [],
            "Check the API service, refresh this workspace, and verify the saved state before retrying."));
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        RefreshButton.IsEnabled = !busy;
        NewIngredientButton.IsEnabled = !busy;
        ReceiveDeliveryButton.IsEnabled = !busy;
        NewSupplierButton.IsEnabled = !busy;
        IngredientsListView.IsEnabled = SearchBox.IsEnabled = UnitFilterBox.IsEnabled = LowStockOnlyCheck.IsEnabled = !busy;
        foreach (var tab in TabsPanel.Children.OfType<Control>()) tab.IsEnabled = !busy;
        DeltaInputBox.IsEnabled = ReasonInputBox.IsEnabled = CancelAdjustmentButton.IsEnabled = !busy;
        ActorButton.IsEnabled = !busy && !App.IsApiMode;
        DrawerOperatorBox.IsEnabled = !busy && !App.IsApiMode;
        ReceiptsListView.IsEnabled = SuppliersListView.IsEnabled = MovementsListView.IsEnabled = HistoryIngredientBox.IsEnabled = !busy;
        ClearFiltersButton.IsEnabled = BalancesEmptyClearButton.IsEnabled = DrawerCloseButton.IsEnabled = !busy;
        SortIngredientButton.IsEnabled = SortOnHandButton.IsEnabled = SortReorderButton.IsEnabled = SortStatusButton.IsEnabled = LastMovementHeaderButton.IsEnabled = !busy;
        RecalculateProjection();
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (isInitialized) ConfigureLayout(e.NewSize.Width, e.NewSize.Height);
    }

    private void OnInventoryTableLayoutUpdated(object? sender, object e)
    {
        if (!isInitialized) return;
        if (compactLayout)
        {
            if (!double.IsNaN(DrawerPanel.Height)) DrawerPanel.Height = double.NaN;
            return;
        }
        if (activeSection != "ingredients" || DrawerPanel.Visibility != Visibility.Visible || InventoryTableEdge.ActualHeight <= 0) return;

        // Follow the visible table edge, including its patterned rule, as rows and window size change.
        var bottom = InventoryTableEdge.TransformToVisual(ContentSplitGrid)
            .TransformPoint(new Windows.Foundation.Point(0, InventoryTableEdge.ActualHeight)).Y;
        if (bottom > 0 && (double.IsNaN(DrawerPanel.Height) || Math.Abs(DrawerPanel.Height - bottom) > 0.5))
            DrawerPanel.Height = bottom;
    }

    private void ConfigureLayout(double width, double height)
    {
        compactLayout = width < 1180 || height < 520;
        // Keep the footer and filter reset reachable on short desktop windows,
        // but let taller windows show more of the ledger.
        IngredientsListView.MaxHeight = compactLayout ? 416 : Math.Max(104, height - 392);
        ContentSplitGrid.ColumnSpacing = compactLayout || !drawerOpen ? 0 : 24;
        RootGrid.Height = compactLayout ? double.NaN : height;
        RootGrid.Padding = new Thickness(compactLayout ? 16 : 32, 24, compactLayout ? 16 : 32, 22);
        RootGrid.RowDefinitions[0].Height = compactLayout ? GridLength.Auto : new GridLength(98);
        RootGrid.RowDefinitions[3].Height = compactLayout ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        ContentSplitGrid.RowDefinitions[0].Height = compactLayout ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        HeaderOrnament.Visibility = compactLayout ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetRow(HeaderActions, compactLayout ? 1 : 0);
        Grid.SetColumn(HeaderActions, compactLayout ? 0 : 2);
        Grid.SetColumnSpan(HeaderActions, compactLayout ? 3 : 1);
        HeaderActions.Margin = new Thickness(0, compactLayout ? 12 : 0, 0, 0);
        HeaderGrid.ColumnDefinitions[1].Width = new GridLength(compactLayout ? 0 : 100);
        HeaderGrid.RowDefinitions[0].Height = compactLayout ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        var wrapFilters = width < 520;
        FilterToolbar.ColumnDefinitions[0].Width = wrapFilters
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(width < 600 ? 140 : 210);
        BalancesView.RowDefinitions[0].Height = new GridLength(wrapFilters ? 100 : 56);
        Grid.SetRow(LowStockOnlyCheck, wrapFilters ? 1 : 0);
        Grid.SetColumn(LowStockOnlyCheck, wrapFilters ? 0 : 2);
        Grid.SetColumnSpan(LowStockOnlyCheck, wrapFilters ? 2 : 1);
        Grid.SetRow(BalancesCountText, wrapFilters ? 1 : 0);
        var rowTemplate = (DataTemplate)Resources[compactLayout ? "StockCompactRow" : "StockWideRow"];
        if (!ReferenceEquals(IngredientsListView.ItemTemplate, rowTemplate))
        {
            // Recreate realized containers when crossing templates; recycled native
            // presenters can otherwise retain the previous template's data context.
            IngredientsListView.ItemsSource = null;
            IngredientsListView.ItemTemplate = rowTemplate;
            ApplyIngredientFilters();
            IngredientsListView.SelectedItem = selectedIngredientRow;
        }
        LastMovementHeaderButton.Visibility = compactLayout ? Visibility.Collapsed : Visibility.Visible;
        var compactHistory = width < 880;
        var movementTemplate = (DataTemplate)Resources[compactHistory ? "StockMovementCompactRow" : "StockMovementWideRow"];
        if (!ReferenceEquals(MovementsListView.ItemTemplate, movementTemplate))
        {
            MovementsListView.ItemTemplate = movementTemplate;
            ApplyHistoryFilter();
        }
        MovementHeaderWide.Visibility = compactHistory ? Visibility.Collapsed : Visibility.Visible;
        MovementHeaderCompact.Visibility = compactHistory ? Visibility.Visible : Visibility.Collapsed;
        TableHeader.ColumnDefinitions[4].Width = compactLayout ? new GridLength(0) : new GridLength(1.1, GridUnitType.Star);
        TableHeader.ColumnDefinitions[0].Width = new GridLength(compactLayout ? 1 : 1.5, GridUnitType.Star);
        for (var i = 1; i <= 3; i++) TableHeader.ColumnDefinitions[i].Width = new GridLength(compactLayout ? (i == 3 ? 64 : 60) : (i == 3 ? 76 : 70));
        if (compactLayout)
        {
            ContentSplitGrid.ColumnDefinitions[1].Width = new GridLength(0);
            Grid.SetColumn(DrawerPanel, 0);
            Grid.SetRow(DrawerPanel, 1);
            DrawerPanel.Margin = new Thickness(0, 16, 0, 0);
        }
        else
        {
            ContentSplitGrid.ColumnDefinitions[1].Width = new GridLength(drawerOpen ? 360 : 0, GridUnitType.Pixel);
            Grid.SetColumn(DrawerPanel, 1);
            Grid.SetRow(DrawerPanel, 0);
            DrawerPanel.Margin = new Thickness(0);
        }
        UpdateEditor();
        UpdateReceiptLayout();
    }

    private void OnOpenHistoryClick(object sender, RoutedEventArgs e) => SelectSection("movements");

    private void OnClearFiltersClick(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        selectedUnitIndex = 0;
        UnitFilterBox.Content = "All units ▾";
        LowStockOnlyCheck.IsChecked = false;
        ApplyIngredientFilters();
    }

    private void OnAdjustmentInputChanged(object sender, TextChangedEventArgs e) => RecalculateProjection();

    private void OnBeginAdjustmentClick(object sender, RoutedEventArgs e)
    {
        if (selectedIngredientRow?.IsActive != true) return;
        isAdjusting = true;
        drawerOpen = true;
        UpdateEditor();
        DeltaInputBox.Focus(FocusState.Programmatic);
    }

    private void OnCancelAdjustmentClick(object sender, RoutedEventArgs e)
    {
        if (selectedIngredientRow is { } row) adjustmentDrafts.Clear(row.Id);
        DeltaInputBox.Text = ReasonInputBox.Text = "";
        isAdjusting = false;
        UpdateEditor();
    }

    private void OnHistoryIngredientChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isInitialized) ApplyHistoryFilter();
    }

    private void SelectHistoryFilter(EntityId? ingredientId)
    {
        if (!isInitialized || historyFilters.Count == 0) return;
        var option = historyFilters.FirstOrDefault(item => item.IngredientId == ingredientId) ?? historyFilters[0];
        if (!ReferenceEquals(HistoryIngredientBox.SelectedItem, option)) HistoryIngredientBox.SelectedItem = option;
        ApplyHistoryFilter();
    }

    private void ApplyHistoryFilter()
    {
        if (!isInitialized) return;
        var filter = (HistoryIngredientBox.SelectedItem as HistoryFilterOption)?.IngredientId;
        var rows = allMovements
            .Where(movement => filter is null || movement.IngredientId == filter)
            .Select(movement =>
            {
                var ingredient = allIngredientRows.FirstOrDefault(row => row.Id == movement.IngredientId);
                return new MovementPresentationRow(
                    movement,
                    ingredient?.Name ?? "Unknown ingredient",
                    ingredient?.BaseUnit ?? string.Empty,
                    staffOptions.FirstOrDefault(staff => staff.Id == movement.ActorId)?.FullName);
            })
            .ToList();
        MovementsListView.ItemsSource = rows;
        HistoryEmptyText.Text = filter is null
            ? "No stock movements have been recorded yet."
            : "No movements recorded for this ingredient yet.";
        HistoryEmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnIngredientClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not IngredientRow row) return;
        SelectIngredient(row);
        drawerOpen = true;
        UpdateEditor();
    }

    private void UpdateEditor()
    {
        if (!isInitialized) return;
        var editing = activeSection == "ingredients" && isAdjusting && selectedIngredientRow is not null;
        var showDrawer = activeSection == "ingredients" && selectedIngredientRow is not null &&
            (compactLayout ? editing : drawerOpen);
        AdjustmentForm.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        AdjustmentActions.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        BeginAdjustmentButton.Visibility = editing || !session.HasPermission("inventory.adjust") ? Visibility.Collapsed : Visibility.Visible;
        RecordAdjustmentButton.Visibility = session.HasPermission("inventory.adjust") ? Visibility.Visible : Visibility.Collapsed;
        DrawerTagsPanel.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        RecentHistoryPanel.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        DrawerKicker.Text = editing ? "ADJUST STOCK" : selectedIngredientRow?.IsActive == false ? "INACTIVE INGREDIENT" : "INGREDIENT DETAILS";
        AutomationProperties.SetName(DrawerCloseButton, editing ? "Close stock adjustment" : "Close ingredient details");
        ToolTipService.SetToolTip(DrawerCloseButton, editing ? "Back to inventory; keep this draft" : "Close ingredient details");
        BeginAdjustmentButton.Content = string.IsNullOrWhiteSpace(DeltaInputBox.Text) && string.IsNullOrWhiteSpace(ReasonInputBox.Text)
            ? "Adjust stock" : "Continue adjustment";
        BeginAdjustmentButton.IsEnabled = !isBusy && selectedIngredientRow?.IsActive == true;
        DrawerPanel.Visibility = showDrawer ? Visibility.Visible : Visibility.Collapsed;
        ContentSplitGrid.ColumnDefinitions[1].Width = new GridLength(showDrawer && !compactLayout ? 360 : 0);
        ContentSplitGrid.ColumnSpacing = showDrawer && !compactLayout ? 24 : 0;
        SectionsHost.Visibility = compactLayout && editing ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void SetTabLabel(ToggleButton tab, string label, int? count = null)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        content.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            CharacterSpacing = 20,
            FontFamily = (FontFamily)XamlApp.Current.Resources["ConsoleSansFont"]
        });
        if (count is { } value) content.Children.Add(new TextBlock
        {
            Text = value.ToString(),
            FontSize = 13,
            FontFamily = (FontFamily)XamlApp.Current.Resources["ConsoleSerifFont"]
        });
        tab.Content = content;
    }

    internal void PrepareReferenceCapture()
    {
        isAdjusting = false;
        UpdateEditor();
        DrawerScrollViewer.ChangeView(null, 0, null, true);
        NewIngredientButton.Focus(FocusState.Programmatic);
    }

    internal void PrepareAdjustmentCapture()
    {
        OnBeginAdjustmentClick(this, new RoutedEventArgs());
        DeltaInputBox.Text = "-120";
        ReasonInputBox.Text = "Grinder calibration waste — morning dial-in.";
        NewIngredientButton.Focus(FocusState.Programmatic);
    }

    internal void ClearCaptureDraft() => OnCancelAdjustmentClick(this, new RoutedEventArgs());

    internal object VerificationState() => new
    {
        selected = selectedIngredientRow?.Id,
        tab = activeSection,
        visibleTabs = TabsPanel.Children.OfType<ToggleButton>().Count(),
        lowStockFilter = LowStockOnlyCheck.IsChecked == true,
        adjusting = isAdjusting,
        canSubmit = RecordAdjustmentButton.IsEnabled,
    };

    internal async Task<object> PrepareVerificationAsync()
    {
        while (isBusy) await Task.Delay(50);
        await LoadAllDataAsync();
        PrepareReferenceCapture();
        return new { ingredients = allIngredientRows.Count, receipts = rawReceipts.Count, suppliers = supplierDirectory.Count, movements = allMovements.Count };
    }

    // Native QA only: exercises local editor state without submitting commands.
    internal async Task<object> VerifyInteractionsAsync()
    {
        var rows = allIngredientRows.Where(row => row.IsActive).Take(2).ToArray();
        if (rows.Length < 2) return new { skipped = true, reason = "Two active ingredients are required." };
        SelectSection("ingredients");
        OnClearFiltersClick(this, new RoutedEventArgs());
        SelectIngredient(rows[0]);
        OnBeginAdjustmentClick(this, new RoutedEventArgs());
        DeltaInputBox.Text = "5";
        ReasonInputBox.Text = "QA count correction";
        await Task.Delay(40);
        RecalculateProjection();
        var validDraftEnabled = RecordAdjustmentButton.IsEnabled;
        SelectIngredient(rows[1]);
        var isolatedDraft = DeltaInputBox.Text == "" && ReasonInputBox.Text == "";
        OnBeginAdjustmentClick(this, new RoutedEventArgs());
        DeltaInputBox.Text = "10";
        ReasonInputBox.Text = "Second ingredient QA draft";
        SelectIngredient(rows[0]);
        OnBeginAdjustmentClick(this, new RoutedEventArgs());
        var restoredDraft = DeltaInputBox.Text == "5" && ReasonInputBox.Text == "QA count correction";
        DeltaInputBox.Text = "-9999999999";
        RecalculateProjection();
        var negativeResultBlocked = !RecordAdjustmentButton.IsEnabled;
        DeltaInputBox.Text = decimal.MaxValue.ToString();
        RecalculateProjection();
        var oversizedQuantityBlocked = !RecordAdjustmentButton.IsEnabled && ProjectedBalanceText.Text == "—";
        DeltaInputBox.Text = "5";
        ReasonInputBox.Text = "";
        RecalculateProjection();
        var reasonOptionalEnabled = RecordAdjustmentButton.IsEnabled;
        OnCancelAdjustmentClick(this, new RoutedEventArgs());
        var cancelIsScoped = adjustmentDrafts.For(rows[0].Id) == new StockAdjustmentDraft()
            && adjustmentDrafts.For(rows[1].Id).Quantity == "10" && !isAdjusting;
        adjustmentDrafts.Clear(rows[1].Id);
        SelectSection("lowstock");
        var lowStockUsesInventory = activeSection == "ingredients" && LowStockOnlyCheck.IsChecked == true;
        OnClearFiltersClick(this, new RoutedEventArgs());
        var filtersCleared = LowStockOnlyCheck.IsChecked == false && selectedUnitIndex == 0 && SearchBox.Text == "";
        SelectIngredient(rows[0]);
        OnOpenHistoryClick(this, new RoutedEventArgs());
        var historyShowsAll = activeSection == "movements" && HistoryIngredientBox.SelectedIndex == 0
            && MovementsListView.Items.Count == allMovements.Count;
        SelectSection("ingredients");
        return new
        {
            skipped = false,
            validDraftEnabled,
            isolatedDraft,
            restoredDraft,
            negativeResultBlocked,
            oversizedQuantityBlocked,
            reasonOptionalEnabled,
            cancelIsScoped,
            lowStockUsesInventory,
            filtersCleared,
            historyShowsAll
        };
    }

    internal bool HasRenderedBalanceName()
    {
        bool ContainsName(DependencyObject element)
        {
            if (element is TextBlock text && allIngredientRows.Any(row => row.Name == text.Text)) return true;
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
                if (ContainsName(VisualTreeHelper.GetChild(element, index))) return true;
            return false;
        }
        return allIngredientRows.Count == 0 || ContainsName(IngredientsListView);
    }
}

public sealed record ReceiptPresentationRow
{
    public ReceiptPresentationRow(
        PurchaseReceipt receipt,
        string supplierName,
        string supplierContact,
        IReadOnlyDictionary<EntityId, Ingredient> ingredients)
    {
        Receipt = receipt;
        SupplierName = supplierName;
        SupplierContact = string.IsNullOrWhiteSpace(supplierContact) ? "No contact recorded" : supplierContact;
        CreatedDisplay = receipt.CreatedAt.Value.ToLocalTime().ToString("d MMM yyyy · HH:mm");
        PostedDisplay = receipt.PostedAt is null
            ? receipt.Status == PurchaseReceiptStatus.Draft ? "Awaiting posting" : "Not posted"
            : receipt.PostedAt.Value.ToLocalTime().ToString("d MMM yyyy · HH:mm");
        Lines = receipt.Lines.Select(line =>
        {
            var found = ingredients.TryGetValue(line.IngredientId, out var ingredient);
            return new ReceiptLineRow(line, found ? ingredient!.Name : "Unknown ingredient", found ? ingredient!.BaseUnit : string.Empty);
        }).ToList();
        TotalDisplay = Lines.Sum(line => line.LineTotal).ToString("0.00");
        LineCountDisplay = $"{Lines.Count} item{(Lines.Count == 1 ? string.Empty : "s")}";
        HasLines = Lines.Count > 0;
        NoLinesVisibility = HasLines ? Visibility.Collapsed : Visibility.Visible;
    }

    public PurchaseReceipt Receipt { get; }

    public EntityId Id => Receipt.Id;
    public string ReceiptNumber => Receipt.ReceiptNumber;
    public string ReceiptTitle => $"Receipt #{ReceiptNumber}";
    public string SupplierName { get; }
    public string SupplierContact { get; }
    public string Status => Receipt.Status.ToString();
    public string CreatedDisplay { get; }
    public string PostedDisplay { get; }
    public string LineCountDisplay { get; }
    public string TotalDisplay { get; }
    public IReadOnlyList<ReceiptLineRow> Lines { get; }
    public bool HasLines { get; }
    public Visibility NoLinesVisibility { get; }
    public bool CanPost => Receipt.Status == PurchaseReceiptStatus.Draft && Receipt.Lines.Count > 0;
    public Visibility PostVisibility => CanPost ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AddLineVisibility => Receipt.Status == PurchaseReceiptStatus.Draft ? Visibility.Visible : Visibility.Collapsed;

    public override string ToString() =>
        $"{ReceiptTitle}, {SupplierName}, {Status}, {CreatedDisplay}, {LineCountDisplay}, {TotalDisplay}";
}

public sealed record HistoryFilterOption(string Name, EntityId? IngredientId);

public sealed record MovementPresentationRow(StockMovement Movement, string IngredientName, string Unit = "", string? ActorName = null)
{
    public string TimeDisplay => IngredientRow.FormatWhen(Movement.OccurredAt.Value.ToLocalTime());
    public string FullTimeDisplay => Movement.OccurredAt.Value.ToLocalTime().ToString("g");
    public string TypeDisplay => Movement.Type.ToString();
    public string DeltaDisplay => $"{Movement.QuantityDelta:+#,0.###;-#,0.###;0} {Unit}".TrimEnd();
    public string ReasonDisplay => Movement.Reason;
    public string ActorDisplay => ActorName ?? Movement.ActorId.ToString();
    public string ContextDisplay => $"{TypeDisplay} · {ActorDisplay}";
    public Brush DeltaBrush => Movement.QuantityDelta >= 0
        ? (Brush)XamlApp.Current.Resources["ConsoleAccentTextBrush"]
        : (Brush)XamlApp.Current.Resources["ConsoleMutedInkBrush"];

    public override string ToString() =>
        $"{TimeDisplay}, {IngredientName}, {TypeDisplay}, {DeltaDisplay}, {ReasonDisplay}, {ActorDisplay}";
}
