using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

/// <summary>
/// Dialog for adding a line to a draft receipt: ingredient picker,
/// quantity, and unit cost.
/// </summary>
public sealed partial class ReceiptLineDialog : ContentDialog
{
    public ReceiptLineDialog(IReadOnlyList<string> ingredientNames)
    {
        InitializeComponent();
        IngredientBox.ItemsSource = ingredientNames;

        Title = "Add receipt line";
        PrimaryButtonText = "Add";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;
    }

    public string? IngredientName => IngredientBox.SelectedItem as string;

    public decimal Quantity { get; private set; }

    public decimal UnitCost { get; private set; }

    private void OnPrimaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (IngredientName is null)
        {
            args.Cancel = true;
            IngredientBox.Header = "Select an ingredient.";
            return;
        }

        if (!decimal.TryParse(QuantityBox.Text, System.Globalization.CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
        {
            args.Cancel = true;
            QuantityBox.Header = "Quantity must be a positive number.";
            return;
        }

        if (!decimal.TryParse(UnitCostBox.Text, System.Globalization.CultureInfo.InvariantCulture, out var unitCost) || unitCost < 0)
        {
            args.Cancel = true;
            UnitCostBox.Header = "Unit cost must be a non-negative number.";
            return;
        }

        Quantity = quantity;
        UnitCost = unitCost;
    }
}
