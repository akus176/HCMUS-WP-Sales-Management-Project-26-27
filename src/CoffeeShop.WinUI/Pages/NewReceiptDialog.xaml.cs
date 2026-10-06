using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

/// <summary>
/// Dialog for creating a draft receipt: supplier picker + receipt number.
/// </summary>
public sealed partial class NewReceiptDialog : ContentDialog
{
    public NewReceiptDialog(IReadOnlyList<string> supplierNames)
    {
        InitializeComponent();
        SupplierBox.ItemsSource = supplierNames;

        Title = "New purchase receipt";
        PrimaryButtonText = "Create";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;
    }

    public string? SupplierName => SupplierBox.SelectedItem as string;

    public string ReceiptNumber => ReceiptNumberBox.Text.Trim();

    private void OnPrimaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (SupplierName is null)
        {
            args.Cancel = true;
            SupplierBox.Header = "Select a supplier.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ReceiptNumber))
        {
            args.Cancel = true;
            ReceiptNumberBox.Header = "Receipt number is required.";
            return;
        }
    }
}
