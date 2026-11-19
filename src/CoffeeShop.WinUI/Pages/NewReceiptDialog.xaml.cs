using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class NewReceiptDialog : ContentDialog
{
    private readonly IReadOnlyList<Ingredient> ingredients;
    private readonly Func<EntityId, string, IReadOnlyList<ReceiveDeliveryLine>, Task<string?>> receive;
    private readonly List<DeliveryRow> rows = [];
    private bool receiving;

    public NewReceiptDialog(IReadOnlyList<Supplier> suppliers, IReadOnlyList<Ingredient> ingredients,
        Func<EntityId, string, IReadOnlyList<ReceiveDeliveryLine>, Task<string?>> receive)
    {
        this.ingredients = ingredients;
        this.receive = receive;
        InitializeComponent();
        SupplierBox.ItemsSource = suppliers;
        SupplierBox.DisplayMemberPath = nameof(Supplier.Name);
        if (suppliers.Count == 1) SupplierBox.SelectedIndex = 0;
        Title = "Receive delivery";
        PrimaryButtonText = "Receive into stock";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;
        AddRow();
    }

    private void OnAddLine(object sender, RoutedEventArgs args)
    {
        var row = AddRow();
        row.Ingredient.Focus(FocusState.Programmatic);
        row.Root.StartBringIntoView();
    }

    private string ReceiptNumber => ReceiptNumberBox.Text.Trim();

    private DeliveryRow AddRow()
    {
        var root = new Grid { ColumnSpacing = 8 };
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var ingredient = new ComboBox
        {
            Header = "Ingredient", PlaceholderText = "Choose ingredient", ItemsSource = ingredients,
            DisplayMemberPath = nameof(Ingredient.Name), HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var quantity = new TextBox { Header = "Quantity", PlaceholderText = "0" };
        var cost = new TextBox { Header = "Unit cost", PlaceholderText = "0" };
        var remove = new Button { Content = "×", VerticalAlignment = VerticalAlignment.Bottom, MinWidth = 32 };
        AutomationProperties.SetName(remove, "Remove ingredient line");
        ingredient.SelectionChanged += (_, _) =>
        {
            quantity.Header = ingredient.SelectedItem is Ingredient item ? $"Qty ({item.BaseUnit})" : "Quantity";
        };
        var row = new DeliveryRow(root, ingredient, quantity, cost, remove);
        remove.Click += (_, _) =>
        {
            rows.Remove(row);
            DeliveryLinesPanel.Children.Remove(root);
            UpdateRemoveButtons();
        };
        Grid.SetColumn(quantity, 1);
        Grid.SetColumn(cost, 2);
        Grid.SetColumn(remove, 3);
        root.Children.Add(ingredient);
        root.Children.Add(quantity);
        root.Children.Add(cost);
        root.Children.Add(remove);
        rows.Add(row);
        DeliveryLinesPanel.Children.Add(root);
        UpdateRemoveButtons();
        return row;
    }

    private void UpdateRemoveButtons()
    {
        foreach (var row in rows) row.Remove.IsEnabled = rows.Count > 1;
    }

    private async void OnPrimaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        DeliveryError.IsOpen = false;
        if (SupplierBox.SelectedItem is not Supplier supplier)
        {
            args.Cancel = true;
            ShowError("Choose the supplier for this delivery.", SupplierBox);
            return;
        }
        if (ReceiptNumber.Length == 0)
        {
            args.Cancel = true;
            ShowError("Enter the receipt number.", ReceiptNumberBox);
            return;
        }

        var lines = new List<ReceiveDeliveryLine>(rows.Count);
        foreach (var row in rows)
        {
            if (row.Ingredient.SelectedItem is not Ingredient ingredient)
            {
                args.Cancel = true;
                ShowError($"Choose an ingredient for line {lines.Count + 1}.", row.Ingredient);
                return;
            }
            if (!CatalogNumberInput.TryParse(row.Quantity.Text, out var quantity) || quantity <= 0
                || quantity > MeasuredQuantity.MaximumValue || decimal.Round(quantity, 3) != quantity)
            {
                args.Cancel = true;
                ShowError($"Enter a positive quantity in {ingredient.BaseUnit} with up to three decimal places for {ingredient.Name}.", row.Quantity);
                return;
            }
            if (!CatalogNumberInput.TryParse(row.Cost.Text, out var cost) || cost < 0 || cost > Money.MaximumAmount
                || decimal.Round(cost, 2) != cost)
            {
                args.Cancel = true;
                ShowError($"Enter a non-negative unit cost with up to two decimal places for {ingredient.Name}.", row.Cost);
                return;
            }
            lines.Add(new(ingredient.Id, quantity, cost));
        }

        var deferral = args.GetDeferral();
        receiving = true;
        IsPrimaryButtonEnabled = false;
        SupplierBox.IsEnabled = ReceiptNumberBox.IsEnabled = AddLineButton.IsEnabled = DeliveryLinesScroll.IsEnabled = false;
        ReceivingProgress.Visibility = Visibility.Visible;
        try
        {
            var error = await receive(supplier.Id, ReceiptNumber, lines);
            if (error is not null)
            {
                args.Cancel = true;
                ShowError(error);
            }
        }
        finally
        {
            ReceivingProgress.Visibility = Visibility.Collapsed;
            SupplierBox.IsEnabled = ReceiptNumberBox.IsEnabled = AddLineButton.IsEnabled = DeliveryLinesScroll.IsEnabled = true;
            IsPrimaryButtonEnabled = true;
            receiving = false;
            deferral.Complete();
        }
    }

    private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args) => args.Cancel = receiving;

    private void ShowError(string message, Control? field = null)
    {
        DeliveryError.Message = message;
        DeliveryError.IsOpen = true;
        field?.Focus(FocusState.Programmatic);
        field?.StartBringIntoView();
    }

    private sealed record DeliveryRow(Grid Root, ComboBox Ingredient, TextBox Quantity, TextBox Cost, Button Remove);
}
