using System.ComponentModel;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class InventoryAuditPage : Page
{
    private readonly InventoryAuditWorkspace model;
    private bool rendering;

    public InventoryAuditPage()
    {
        InitializeComponent();
        model = App.Services.GetRequiredService<InventoryAuditWorkspace>();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        model.PropertyChanged += OnChanged;
        await model.LoadAsync();
    }
    private void OnUnloaded(object sender, RoutedEventArgs args) => model.PropertyChanged -= OnChanged;
    private void OnChanged(object? sender, PropertyChangedEventArgs args) => Render();
    private async void OnReload(object sender, RoutedEventArgs args) => await model.LoadAsync();
    private async void OnIngredientChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!rendering && IngredientBox.SelectedItem is Ingredient ingredient) await model.SelectAsync(ingredient.Id);
    }
    private void OnOperatorChanged(object sender, SelectionChangedEventArgs args)
    {
        if (rendering) return;
        model.Session.Operator = OperatorBox.SelectedItem as PosStaffOption;
        Render();
    }
    private async void OnAdjust(object sender, RoutedEventArgs args)
    {
        if (!CatalogNumberInput.TryParse(DeltaBox.Text, out var delta))
        {
            model.ShowValidation("Enter a quantity using your local decimal separator, without grouping separators.");
            return;
        }
        await model.AdjustAsync(delta, ReasonBox.Text);
    }
    private void Render()
    {
        rendering = true;
        Status.Apply(model.Feedback);
        LoadingBar.Visibility = model.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        ReloadButton.IsEnabled = IngredientBox.IsEnabled = OperatorBox.IsEnabled = !model.IsBusy;
        DeltaBox.IsEnabled = ReasonBox.IsEnabled = !model.IsBusy && !model.RequiresReload;
        AdjustButton.IsEnabled = model.CanAdjust;
        IngredientBox.ItemsSource = model.Ingredients;
        IngredientBox.SelectedItem = model.SelectedIngredient;
        OperatorBox.ItemsSource = model.Staff;
        OperatorBox.SelectedItem = model.Staff.SingleOrDefault(staff => staff.Id == model.Session.Id);
        BalanceText.Text = model.SelectedIngredient is { } ingredient
            ? $"Balance: {ingredient.AvailableQuantity:0.###} {ingredient.BaseUnit}; reorder threshold: {ingredient.ReorderThreshold:0.###}."
            : "Create an ingredient to begin.";
        HistoryList.ItemsSource = model.Movements.Select(movement => new TextBlock
        {
            Text = $"{movement.OccurredAt.Value:u} | {movement.Type} | {movement.QuantityDelta:+0.###;-0.###} | {movement.Reason}\nActor: {movement.ActorId} | Order: {movement.OrderId} | Receipt: {movement.PurchaseReceiptId}",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 8),
            IsTextSelectionEnabled = true
        }).ToArray();
        EmptyText.Visibility = model.Movements.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        rendering = false;
    }
}
