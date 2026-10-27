using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Sales;
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
    private string activeSection = "ingredients";
    private bool isBusy;

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
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadAllDataAsync();
        UpdateActiveTabVisuals();
    }

    public async Task RefreshDataAsync() => await LoadAllDataAsync();

    private async Task LoadAllDataAsync()
    {
        if (isBusy) return;
        SetBusy(true);
        ShowFeedback(WorkflowFeedback.Loading("Syncing inventory…"));
        try
        {
            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
            var rawIngredients = (await repo.ListIngredientsAsync()).ToList();
            var lowCount = rawIngredients.Count(i => i.Status == StockStatus.LowStock);
            TabBalances.Content = $"BALANCES ({rawIngredients.Count})";
            TabLowStock.Content = $"LOW STOCK ({lowCount})";
            TabMovements.Content = "MOVEMENTS";
            SyncPillText.Text = $"SYNCED {DateTime.Now:HH:mm}";
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

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            SelectSection(tag);
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await LoadAllDataAsync();

    private void OnNewIngredientClick(object sender, RoutedEventArgs e) { }
    private void OnReceiveDeliveryClick(object sender, RoutedEventArgs e) { }
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
    }
}
