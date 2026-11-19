using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

/// <summary>
/// Create/edit dialog for ingredients. Exposes parsed values as properties
/// so the calling page can build commands without re-reading controls.
/// </summary>
public sealed partial class IngredientDialog : ContentDialog
{
    private readonly Ingredient? editing;

    public IngredientDialog(Ingredient? ingredient = null)
    {
        InitializeComponent();
        editing = ingredient;

        Title = ingredient is null ? "New ingredient" : "Edit ingredient";
        PrimaryButtonText = ingredient is null ? "Create" : "Save";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        if (ingredient is not null)
        {
            NameBox.Text = ingredient.Name;
            NameBox.IsEnabled = false; // name is identity-like; keep edits to numbers
            UnitBox.SelectedItem = UnitBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, ingredient.BaseUnit));
            UnitBox.IsEnabled = false;
            BalanceBox.Text = ingredient.AvailableQuantity.ToString("0.###");
            BalanceBox.IsEnabled = false; // balance changes flow through receipts/adjustments
            ThresholdBox.Text = ingredient.ReorderThreshold.ToString("0.###");
        }
    }

    public string IngredientName => NameBox.Text.Trim();

    public string BaseUnit => (UnitBox.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;

    public decimal AvailableQuantity { get; private set; }

    public decimal ReorderThreshold { get; private set; }

    private void OnUnitChanged(object sender, SelectionChangedEventArgs args)
    {
        if (BalanceBox is null || ThresholdBox is null) return;
        UnitBox.Header = "Stock unit";
        var suffix = BaseUnit.Length > 0 ? $" ({BaseUnit})" : "";
        BalanceBox.Header = "Starting stock" + suffix;
        ThresholdBox.Header = "Low-stock threshold" + suffix;
    }

    private void OnPrimaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (editing is null)
        {
            if (string.IsNullOrWhiteSpace(IngredientName))
            {
                args.Cancel = true;
                ShowFieldError(NameBox, "Name is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(BaseUnit))
            {
                args.Cancel = true;
                UnitBox.Header = "Choose a stock unit.";
                return;
            }

            if (!TryParseDecimal(BalanceBox.Text, out var balance) || balance < 0)
            {
                args.Cancel = true;
                ShowFieldError(BalanceBox, "Balance must be a non-negative number.");
                return;
            }

            if (!TryParseDecimal(ThresholdBox.Text, out var threshold) || threshold < 0)
            {
                args.Cancel = true;
                ShowFieldError(ThresholdBox, "Reorder threshold must be a non-negative number.");
                return;
            }

            AvailableQuantity = balance;
            ReorderThreshold = threshold;
        }
        else
        {
            if (!TryParseDecimal(ThresholdBox.Text, out var threshold) || threshold < 0)
            {
                args.Cancel = true;
                ShowFieldError(ThresholdBox, "Reorder threshold must be a non-negative number.");
                return;
            }

            ReorderThreshold = threshold;
        }
    }

    private static bool TryParseDecimal(string text, out decimal value) =>
        CatalogNumberInput.TryParse(text, out value) && value <= MeasuredQuantity.MaximumValue && decimal.Round(value, 3) == value;

    private static void ShowFieldError(TextBox box, string message)
    {
        box.Header = message;
    }
}
