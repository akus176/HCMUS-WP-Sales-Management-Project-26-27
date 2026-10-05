using CoffeeShop.Domain.Inventory;
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
            UnitBox.Text = ingredient.BaseUnit;
            UnitBox.IsEnabled = false;
            BalanceBox.Text = ingredient.AvailableQuantity.ToString("0.###");
            BalanceBox.IsEnabled = false; // balance changes flow through receipts/adjustments
            ThresholdBox.Text = ingredient.ReorderThreshold.ToString("0.###");
        }
    }

    public string IngredientName => NameBox.Text.Trim();

    public string BaseUnit => UnitBox.Text.Trim().ToLowerInvariant();

    public decimal AvailableQuantity { get; private set; }

    public decimal ReorderThreshold { get; private set; }

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
                ShowFieldError(UnitBox, "Base unit is required (g, kg, ml, l, pcs).");
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
        decimal.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out value);

    private static void ShowFieldError(TextBox box, string message)
    {
        box.Header = message;
    }
}
